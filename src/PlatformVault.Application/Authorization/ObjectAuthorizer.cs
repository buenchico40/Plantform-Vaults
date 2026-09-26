using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Objects;
using PlatformVault.Domain.Identity;

namespace PlatformVault.Application.Authorization;

/// <summary>Operaciones sobre un objeto sujetas a autorización (matriz de permisos §4.2).</summary>
public enum ObjectOperation
{
    View,
    EditMetadata,
    Reclassify,
    EditValue,
    ChangeState,
    ManageOwners,
    ManageGroups,
    RequestAccess,
    ViewAudit,
}

/// <summary>
/// Autorización por objeto (IMP-08, RN-010, RN-040). Fuera de ámbito responde 404 y audita un posible IDOR/BOLA;
/// visible pero sin permiso responde 403. Los controles visuales de la Web nunca sustituyen esta decisión.
/// </summary>
public sealed class ObjectAuthorizer(IObjectRepository objects, ICurrentUser user, IClock clock, AuditLogger audit)
{
    public async Task<ObjectAuthorizationContext> AuthorizeAsync(Guid objectId, ObjectOperation operation, CancellationToken ct)
    {
        var context = await objects.GetAuthorizationContextAsync(objectId, user.UserId, clock.UtcNow, ct);
        if (context is null || !IsVisible(context, user))
        {
            await audit.DeniedAsync(AuditActions.ObjectAccessDenied, "ManagedObject", objectId.ToString(),
                new { operation = operation.ToString(), reason = context is null ? "NotFound" : "OutOfScope" }, ct);
            throw new NotFoundFailure("objeto");
        }
        if (!IsAllowed(context, user, operation))
        {
            await audit.DeniedAsync(AuditActions.ObjectAccessDenied, "ManagedObject", objectId.ToString(),
                new { operation = operation.ToString(), reason = "NotPermitted" }, ct);
            throw new ForbiddenFailure();
        }
        return context;
    }

    public static bool IsVisible(ObjectAuthorizationContext context, ICurrentUser user) =>
        user.HasGlobalScope() || HasScopedAccess(context, user);

    /// <summary>Acceso por ámbito no global: propiedad, área del custodio o grupo activo (RN-010).</summary>
    public static bool HasScopedAccess(ObjectAuthorizationContext context, ICurrentUser user) =>
        context.IsOwner(user.UserId)
        || (user.IsInRole(SystemRoles.Custodian) && user.AreaId == context.AreaId)
        || context.IsGroupMember;

    public static bool IsAllowed(ObjectAuthorizationContext context, ICurrentUser user, ObjectOperation operation)
    {
        var scoped = HasScopedAccess(context, user);
        var owner = context.IsOwner(user.UserId);
        var custodian = user.IsInRole(SystemRoles.Custodian) && scoped;
        var operatorMember = user.IsInRole(SystemRoles.Operator) && context.IsGroupMember;

        return operation switch
        {
            ObjectOperation.View => IsVisible(context, user),
            ObjectOperation.EditMetadata => custodian || owner,
            ObjectOperation.Reclassify => custodian || owner || user.IsInRole(SystemRoles.Security),
            ObjectOperation.EditValue => custodian || context.TechnicalOwnerId == user.UserId || operatorMember,
            ObjectOperation.ChangeState => custodian || owner,
            ObjectOperation.ManageOwners => custodian,
            ObjectOperation.ManageGroups => custodian || owner,
            // Matriz §4.2: revelan/descargan Custodio, Propietario y Operador mediante Temporary Access.
            // Auditor y Seguridad no pueden ser miembros ni propietarios, por lo que no tienen acceso por ámbito.
            ObjectOperation.RequestAccess => scoped && (owner || user.IsInRole(SystemRoles.Custodian) || user.IsInRole(SystemRoles.Operator)),
            ObjectOperation.ViewAudit => user.IsInRole(SystemRoles.Auditor) || user.IsInRole(SystemRoles.Security) || custodian || owner,
            _ => false,
        };
    }
}
