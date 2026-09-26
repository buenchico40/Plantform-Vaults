using System.Text.Json;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;
using PlatformVault.Application.Common;
using PlatformVault.Application.Groups;
using PlatformVault.Application.Notifications;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Identity;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Objects;

public sealed record UpdateObjectCommand(Guid ObjectId, string ETag, string Name, string? Description, DateTime? ExpirationDate,
    bool NoExpirationJustified, IReadOnlyDictionary<string, string>? Attributes, string Reason);

public sealed record ReclassifyObjectCommand(Guid ObjectId, string ETag, Criticality Criticality, Sensitivity Sensitivity, string Reason);

public enum StateAction
{
    Activate,
    Suspend,
    Deactivate,
    Reactivate,
}

public sealed record ChangeObjectStateCommand(Guid ObjectId, string ETag, StateAction Action, string Reason);

public sealed record AssignOwnerCommand(Guid ObjectId, string ETag, Guid OwnerId, string Reason);

public sealed record SetObjectGroupsCommand(Guid ObjectId, string ETag, IReadOnlyList<Guid> GroupIds, string Reason);

/// <summary>Carga, autorización y concurrencia comunes a los comandos de mantenimiento.</summary>
public sealed class ObjectCommandContext(ObjectAuthorizer authorizer, IObjectRepository objects)
{
    public async Task<(ObjectAuthorizationContext Context, ManagedObject Object, byte[] RowVer)> LoadAsync(
        Guid objectId, string etag, ObjectOperation operation, CancellationToken ct)
    {
        var rowVer = ETag.Parse(etag);
        var context = await authorizer.AuthorizeAsync(objectId, operation, ct);
        var obj = await objects.LoadAsync(objectId, ct) ?? throw new NotFoundFailure("objeto");
        if (!obj.RowVer.AsSpan().SequenceEqual(rowVer))
            throw new ConcurrencyFailure();
        return (context, obj, rowVer);
    }

    public static string Fields(IEnumerable<string> fields) => JsonSerializer.Serialize(fields);
}

/// <summary>US-006: edición de metadatos con nueva versión (RN-092).</summary>
public sealed class UpdateObjectHandler(ICurrentUser user, IClock clock, ObjectCommandContext loader, IObjectRepository objects,
    IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier) : ICommandHandler<UpdateObjectCommand, ObjectWriteResult>
{
    public async Task<ObjectWriteResult> HandleAsync(UpdateObjectCommand command, CancellationToken ct)
    {
        var reason = RequireReason(command.Reason);
        var (_, obj, rowVer) = await loader.LoadAsync(command.ObjectId, command.ETag, ObjectOperation.EditMetadata, ct);
        var previousExpiration = obj.ExpirationDate;
        var previousName = obj.Name;
        var changed = obj.UpdateMetadata(new MetadataChange(command.Name, command.Description, command.ExpirationDate,
            command.NoExpirationJustified, command.Attributes));
        if (changed.Count == 0)
            return new ObjectWriteResult(ETag.From(obj.RowVer), obj.CurrentVersion);

        if (!string.Equals(previousName, obj.Name, StringComparison.Ordinal))
        {
            var (nameExists, _) = await objects.ExistsDuplicateAsync(obj.ObjectType, obj.Environment, obj.AreaId, obj.Name, null, obj.ObjectId, ct);
            if (nameExists)
                throw new ConflictFailure("DUPLICATE_NAME", "Ya existe un objeto con ese nombre para el mismo tipo, ambiente y área (RN-005).");
        }

        await using var tx = await unitOfWork.BeginAsync(ct);
        var result = await objects.UpdateMetadataAsync(obj, rowVer, user.UserId, clock.UtcNow, reason, ObjectCommandContext.Fields(changed),
            resolveOpenAlerts: previousExpiration != obj.ExpirationDate, ct);
        await audit.SuccessAsync(AuditActions.ObjectUpdated, "ManagedObject", obj.ObjectId.ToString(),
            new { code = obj.Code, version = result.CurrentVersion, changedFields = changed, reason }, ct);
        await notifier.NotifySecurityAsync(obj.IsCritical, obj.Code, "edición de metadatos", ct);
        await notifier.NotifyGroupActivityAsync(obj.ObjectId, obj.Code, "modificar", ct);
        await tx.CommitAsync(ct);
        return result;
    }

    internal static string RequireReason(string? reason)
    {
        var clean = reason?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length < 5)
            throw new DomainException(DomainErrors.ReasonRequired, "El motivo es obligatorio (mínimo 5 caracteres).");
        return clean.Length <= 500 ? clean : throw new DomainException(DomainErrors.InvalidValue, "El motivo admite como máximo 500 caracteres.");
    }
}

/// <summary>PUT /classification. Rebajar un objeto Crítico solo lo hace Seguridad (RN-107).</summary>
public sealed class ReclassifyObjectHandler(ICurrentUser user, IClock clock, ObjectCommandContext loader, IObjectRepository objects,
    IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier) : ICommandHandler<ReclassifyObjectCommand, ObjectWriteResult>
{
    public async Task<ObjectWriteResult> HandleAsync(ReclassifyObjectCommand command, CancellationToken ct)
    {
        var reason = UpdateObjectHandler.RequireReason(command.Reason);
        var (context, obj, rowVer) = await loader.LoadAsync(command.ObjectId, command.ETag, ObjectOperation.Reclassify, ct);
        var wasCritical = obj.IsCritical;
        var changed = obj.Reclassify(command.Criticality, command.Sensitivity, user.IsInRole(SystemRoles.Security));
        if (changed.Count == 0)
            return new ObjectWriteResult(ETag.From(obj.RowVer), obj.CurrentVersion);

        if (obj.LifecycleState == LifecycleState.Active && (obj.IsCritical || obj.Sensitivity == Sensitivity.Restricted)
            && context.MaxActiveMembersInAGroup < 2)
            throw new DomainException(DomainErrors.GroupRequirement,
                "Un objeto activo Crítico o Restringido necesita un grupo con dos o más miembros activos (RN-104).");

        await using var tx = await unitOfWork.BeginAsync(ct);
        var result = await objects.UpdateMetadataAsync(obj, rowVer, user.UserId, clock.UtcNow, reason, ObjectCommandContext.Fields(changed),
            resolveOpenAlerts: false, ct);
        await audit.SuccessAsync(AuditActions.ObjectReclassified, "ManagedObject", obj.ObjectId.ToString(), new
        {
            code = obj.Code,
            version = result.CurrentVersion,
            criticality = obj.Criticality.ToString(),
            sensitivity = obj.Sensitivity.ToString(),
            reason,
        }, ct);
        await notifier.NotifySecurityAsync(wasCritical || obj.IsCritical, obj.Code, "cambio de clasificación", ct);
        await tx.CommitAsync(ct);
        return result;
    }
}

/// <summary>US-007: cambio de estado del ciclo de vida (RN-007, RN-009).</summary>
public sealed class ChangeObjectStateHandler(ICurrentUser user, IClock clock, ObjectCommandContext loader, IObjectRepository objects,
    IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier) : ICommandHandler<ChangeObjectStateCommand, StateChangeResult>
{
    public async Task<StateChangeResult> HandleAsync(ChangeObjectStateCommand command, CancellationToken ct)
    {
        var reason = UpdateObjectHandler.RequireReason(command.Reason);
        var (context, obj, rowVer) = await loader.LoadAsync(command.ObjectId, command.ETag, ObjectOperation.ChangeState, ct);
        var previous = obj.LifecycleState;
        var target = command.Action switch
        {
            StateAction.Activate or StateAction.Reactivate => LifecycleState.Active,
            StateAction.Suspend => LifecycleState.Suspended,
            StateAction.Deactivate => LifecycleState.Deactivated,
            _ => throw new ValidationFailure("Acción de estado no válida."),
        };
        obj.ChangeState(target, new GroupCoverage(context.Groups.Count(g => g.IsActive), context.MaxActiveMembersInAGroup));

        await using var tx = await unitOfWork.BeginAsync(ct);
        var result = await objects.ChangeStateAsync(obj.ObjectId, target, rowVer, user.UserId, clock.UtcNow, reason, ct);
        await audit.SuccessAsync(AuditActions.ObjectStateChanged, "ManagedObject", obj.ObjectId.ToString(), new
        {
            code = obj.Code,
            from = previous.ToString(),
            to = target.ToString(),
            revokedAccesses = result.RevokedAccesses,
            cancelledRequests = result.CancelledRequests,
            reason,
        }, ct);
        await notifier.NotifySecurityAsync(obj.IsCritical, obj.Code, $"cambio de estado a {target}", ct);
        await notifier.NotifyGroupActivityAsync(obj.ObjectId, obj.Code, "cambiar estado", ct);
        await tx.CommitAsync(ct);
        return result;
    }
}

/// <summary>US-013: asignación del propietario con historial (RN-087, RN-089, IMP-62).</summary>
public sealed class AssignOwnerHandler(ICurrentUser user, IClock clock, ObjectCommandContext loader, IObjectRepository objects,
    OwnerValidator owners, IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier) : ICommandHandler<AssignOwnerCommand, ObjectWriteResult>
{
    public async Task<ObjectWriteResult> HandleAsync(AssignOwnerCommand command, CancellationToken ct)
    {
        var reason = UpdateObjectHandler.RequireReason(command.Reason);
        var (_, obj, rowVer) = await loader.LoadAsync(command.ObjectId, command.ETag, ObjectOperation.ManageOwners, ct);
        await owners.ValidateAsync(command.OwnerId, ct);
        obj.AssignOwner(command.OwnerId);

        await using var tx = await unitOfWork.BeginAsync(ct);
        var result = await objects.SetOwnerAsync(obj.ObjectId, command.OwnerId, rowVer, user.UserId,
            clock.UtcNow, reason, ct);
        await audit.SuccessAsync(AuditActions.ObjectOwnersChanged, "ManagedObject", obj.ObjectId.ToString(), new
        {
            code = obj.Code,
            ownerId = command.OwnerId,
            reason,
        }, ct);
        await notifier.NotifySecurityAsync(obj.IsCritical, obj.Code, "cambio de propietario", ct);
        await tx.CommitAsync(ct);
        return result;
    }
}

/// <summary>US-028: asignación del objeto a grupos de acceso (RN-104).</summary>
public sealed class SetObjectGroupsHandler(ICurrentUser user, IClock clock, ObjectCommandContext loader, IObjectRepository objects,
    IGroupRepository groups, IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier) : ICommandHandler<SetObjectGroupsCommand, ObjectWriteResult>
{
    public const int MaxGroups = 20;

    public async Task<ObjectWriteResult> HandleAsync(SetObjectGroupsCommand command, CancellationToken ct)
    {
        var reason = UpdateObjectHandler.RequireReason(command.Reason);
        var ids = command.GroupIds.Distinct().ToList();
        if (ids.Count > MaxGroups)
            throw new ValidationFailure($"Un objeto admite como máximo {MaxGroups} grupos.");
        var (_, obj, rowVer) = await loader.LoadAsync(command.ObjectId, command.ETag, ObjectOperation.ManageGroups, ct);

        IReadOnlyList<GroupCapacity> capacities = ids.Count == 0 ? [] : await groups.GetCapacitiesAsync(ids, ct);
        if (capacities.Count != ids.Count)
            throw new ValidationFailure("Alguno de los grupos no existe.");
        if (capacities.Any(g => !g.IsActive))
            throw new DomainException(DomainErrors.GroupRequirement, "No se puede asignar un grupo inactivo.");
        if ((obj.IsCritical || obj.Sensitivity == Sensitivity.Restricted) && obj.LifecycleState == LifecycleState.Active
            && !capacities.Any(g => g.ActiveMemberCount >= 2))
            throw new DomainException(DomainErrors.GroupRequirement,
                "Un objeto activo Crítico o Restringido debe quedar en al menos un grupo con dos o más miembros activos (RN-104).");

        await using var tx = await unitOfWork.BeginAsync(ct);
        var result = await objects.SetGroupsAsync(obj.ObjectId, ids, rowVer, user.UserId, clock.UtcNow, reason, ct);
        await audit.SuccessAsync(AuditActions.ObjectGroupsChanged, "ManagedObject", obj.ObjectId.ToString(),
            new { code = obj.Code, groups = capacities.Select(g => g.Code), reason }, ct);
        await notifier.NotifySecurityAsync(obj.IsCritical, obj.Code, "cambio de grupos", ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
