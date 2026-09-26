using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;
using PlatformVault.Application.Notifications;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Identity;
using PlatformVault.Domain.Security;

namespace PlatformVault.Application.Groups;

public sealed record CreateGroupCommand(string Name, string? Description, Guid? AreaId, Guid ResponsibleUserId);

public sealed record UpdateGroupCommand(Guid GroupId, string Name, string? Description, bool IsActive);

public sealed record AddGroupMemberCommand(Guid GroupId, Guid UserId, bool IsResponsible);

public sealed record RemoveGroupMemberCommand(Guid GroupId, Guid UserId);

public sealed record GetGroupQuery(Guid GroupId);

public sealed record SearchGroupsQuery(string? Text, bool? IsActive, PageRequest Page);

public sealed record GroupCreated(Guid Id, string Code);

internal static class GroupRules
{
    public static (string Name, string? Description) Clean(string? name, string? description)
    {
        var cleanName = name?.Trim();
        if (string.IsNullOrEmpty(cleanName) || cleanName.Length > 150)
            throw new ValidationFailure("El nombre del grupo es obligatorio (máximo 150 caracteres).");
        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (cleanDescription is { Length: > 500 })
            throw new ValidationFailure("La descripción admite como máximo 500 caracteres.");
        if (CardDataDetector.ContainsCardNumber(cleanName) || CardDataDetector.ContainsCardNumber(cleanDescription))
            throw new DomainException(DomainErrors.CardDataDetected, "El grupo no puede contener datos de tarjeta (RN-124).");
        return (cleanName, cleanDescription);
    }

    public static async Task EnsureEligibleMemberAsync(IUserDirectory users, Guid userId, CancellationToken ct)
    {
        var member = await users.GetAsync(userId, ct) ?? throw new NotFoundFailure("usuario");
        SegregationOfDuties.EnsureCanBeGroupMember(member.Roles, member.IsActive);
    }

    /// <summary>RN-034: gestionan miembros el Administrador y los Responsables del grupo.</summary>
    public static async Task<GroupDetail> LoadForMembershipAsync(IGroupRepository groups, ICurrentUser user, Guid groupId, CancellationToken ct)
    {
        var group = await groups.GetAsync(groupId, user.UserId, hasGlobalScope: true, ct) ?? throw new NotFoundFailure("grupo");
        var isResponsible = group.Members.Any(m => m.UserId == user.UserId && m.IsResponsible);
        if (!user.Has(Permission.ManageGroups) && !isResponsible)
        {
            if (group.Members.All(m => m.UserId != user.UserId) && !user.HasGlobalScope())
                throw new NotFoundFailure("grupo");
            throw new ForbiddenFailure("Solo el Administrador o un Responsable del grupo gestionan sus miembros (RN-034).");
        }
        if (!group.IsActive)
            throw new ConflictFailure("GROUP_INACTIVE", "El grupo está inactivo.");
        return group;
    }
}

/// <summary>US-027: alta de grupo por el Administrador con su Responsable (RN-033, RN-034).</summary>
public sealed class CreateGroupHandler(ICurrentUser user, IClock clock, IGroupRepository groups, IUserDirectory users, IUnitOfWork unitOfWork,
    AuditLogger audit) : ICommandHandler<CreateGroupCommand, GroupCreated>
{
    public async Task<GroupCreated> HandleAsync(CreateGroupCommand command, CancellationToken ct)
    {
        user.Require(Permission.ManageGroups);
        var (name, description) = GroupRules.Clean(command.Name, command.Description);
        await GroupRules.EnsureEligibleMemberAsync(users, command.ResponsibleUserId, ct);

        var groupId = Guid.NewGuid();
        await using var tx = await unitOfWork.BeginAsync(ct);
        var code = await groups.InsertAsync(groupId, name, description, command.AreaId, command.ResponsibleUserId, user.UserId, clock.UtcNow, ct);
        await audit.SuccessAsync(AuditActions.GroupCreated, "SecurityGroup", groupId.ToString(),
            new { code, name, responsible = command.ResponsibleUserId }, ct);
        await tx.CommitAsync(ct);
        return new GroupCreated(groupId, code);
    }
}

/// <summary>US-027: edición y desactivación (RN-035).</summary>
public sealed class UpdateGroupHandler(ICurrentUser user, IClock clock, IGroupRepository groups, IUnitOfWork unitOfWork, AuditLogger audit)
    : ICommandHandler<UpdateGroupCommand, MembershipChangeResult>
{
    public async Task<MembershipChangeResult> HandleAsync(UpdateGroupCommand command, CancellationToken ct)
    {
        user.Require(Permission.ManageGroups);
        var (name, description) = GroupRules.Clean(command.Name, command.Description);
        _ = await groups.GetAsync(command.GroupId, user.UserId, hasGlobalScope: true, ct) ?? throw new NotFoundFailure("grupo");

        await using var tx = await unitOfWork.BeginAsync(ct);
        var revoked = await groups.UpdateAsync(command.GroupId, name, description, command.IsActive, user.UserId, clock.UtcNow, ct);
        await audit.SuccessAsync(AuditActions.GroupUpdated, "SecurityGroup", command.GroupId.ToString(),
            new { name, isActive = command.IsActive, revokedAccesses = revoked }, ct);
        await tx.CommitAsync(ct);
        return new MembershipChangeResult(revoked, 0);
    }
}

/// <summary>US-027: alta de miembro o cambio de Responsable (RN-034, RN-103).</summary>
public sealed class AddGroupMemberHandler(ICurrentUser user, IClock clock, IGroupRepository groups, IUserDirectory users,
    IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier) : ICommandHandler<AddGroupMemberCommand, Unit>
{
    public async Task<Unit> HandleAsync(AddGroupMemberCommand command, CancellationToken ct)
    {
        var group = await GroupRules.LoadForMembershipAsync(groups, user, command.GroupId, ct);
        await GroupRules.EnsureEligibleMemberAsync(users, command.UserId, ct);

        await using var tx = await unitOfWork.BeginAsync(ct);
        await groups.UpsertMemberAsync(command.GroupId, command.UserId, command.IsResponsible, user.UserId, clock.UtcNow, ct);
        await audit.SuccessAsync(AuditActions.GroupMemberAdded, "SecurityGroup", command.GroupId.ToString(),
            new { group = group.Code, userId = command.UserId, responsible = command.IsResponsible }, ct);
        var recipients = await users.GetContactsAsync([.. group.Members.Select(m => m.UserId), command.UserId], ct);
        await notifier.NotifyAsync(recipients.Where(r => r.UserId != user.UserId), $"Cambio de miembros en {group.Code}",
            $"{user.DisplayName} actualizó la pertenencia al grupo {group.Name}.", ct);
        await tx.CommitAsync(ct);
        return Unit.Value;
    }
}

/// <summary>US-027: retiro de miembro con revocación inmediata de accesos derivados (RN-035).</summary>
public sealed class RemoveGroupMemberHandler(ICurrentUser user, IClock clock, IGroupRepository groups, IUserDirectory users,
    IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier) : ICommandHandler<RemoveGroupMemberCommand, MembershipChangeResult>
{
    public async Task<MembershipChangeResult> HandleAsync(RemoveGroupMemberCommand command, CancellationToken ct)
    {
        var group = await GroupRules.LoadForMembershipAsync(groups, user, command.GroupId, ct);

        await using var tx = await unitOfWork.BeginAsync(ct);
        var result = await groups.RemoveMemberAsync(command.GroupId, command.UserId, user.UserId, clock.UtcNow, ct);
        await audit.SuccessAsync(AuditActions.GroupMemberRemoved, "SecurityGroup", command.GroupId.ToString(),
            new { group = group.Code, userId = command.UserId, result.RevokedAccesses, result.CancelledRequests }, ct);
        var recipients = await users.GetContactsAsync([.. group.Members.Select(m => m.UserId)], ct);
        var remaining = group.Members.Count(m => m.IsActive && m.UserId != command.UserId);
        var warning = remaining < 2
            ? " El grupo queda con menos de dos miembros activos: sus objetos Críticos o Restringidos quedan bloqueados para revelado y descarga (RN-104)."
            : string.Empty;
        await notifier.NotifyAsync(recipients.Where(r => r.UserId != user.UserId), $"Cambio de miembros en {group.Code}",
            $"{user.DisplayName} retiró a un miembro del grupo {group.Name}.{warning}", ct);
        if (remaining < 2)
            await notifier.NotifyAsync(await users.GetActiveByRoleAsync(SystemRoles.Administrator, ct), $"Grupo {group.Code} con un solo miembro",
                $"El grupo {group.Name} tiene menos de dos miembros activos (RN-104).", ct);
        await tx.CommitAsync(ct);
        return result;
    }
}

public sealed class GetGroupHandler(ICurrentUser user, IGroupRepository groups) : IQueryHandler<GetGroupQuery, GroupDetail>
{
    public async Task<GroupDetail> HandleAsync(GetGroupQuery query, CancellationToken ct) =>
        await groups.GetAsync(query.GroupId, user.UserId, user.HasGlobalScope(), ct) ?? throw new NotFoundFailure("grupo");
}

public sealed class SearchGroupsHandler(ICurrentUser user, IGroupRepository groups) : IQueryHandler<SearchGroupsQuery, PagedResult<GroupSummary>>
{
    public Task<PagedResult<GroupSummary>> HandleAsync(SearchGroupsQuery query, CancellationToken ct) =>
        groups.SearchAsync(query.Text, query.IsActive, query.Page, user.UserId, user.HasGlobalScope(), ct);
}
