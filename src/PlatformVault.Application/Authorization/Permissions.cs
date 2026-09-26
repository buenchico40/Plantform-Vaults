using PlatformVault.Application.Abstractions;
using PlatformVault.Domain.Identity;

namespace PlatformVault.Application.Authorization;

/// <summary>Permisos funcionales (no dependen de un objeto concreto). Denegación por defecto (RN-040).</summary>
public enum Permission
{
    ViewInventory,
    CreateObject,
    ManageGroups,
    ManageUsers,
    ManageAreas,
    ManageExpirationPolicies,
    ViewAudit,
    VerifyAuditChain,
    ViewAlerts,
    ViewJobs,
    ApproveCriticalAccess,
    RequestAccess,
}

public static class Permissions
{
    private static readonly Dictionary<string, Permission[]> ByRole = new(StringComparer.Ordinal)
    {
        [SystemRoles.Administrator] = [Permission.ViewInventory, Permission.ManageGroups, Permission.ManageUsers, Permission.ManageAreas,
            Permission.ViewJobs, Permission.ViewAlerts],
        [SystemRoles.Custodian] = [Permission.ViewInventory, Permission.CreateObject, Permission.ViewAudit, Permission.ViewAlerts,
            Permission.RequestAccess],
        // IMP-46 (aprobada 2026-09-25): el Operador también registra objetos en su área.
        [SystemRoles.Operator] = [Permission.ViewInventory, Permission.CreateObject, Permission.ViewAlerts, Permission.RequestAccess],
        [SystemRoles.Auditor] = [Permission.ViewInventory, Permission.ViewAudit, Permission.VerifyAuditChain, Permission.ViewAlerts],
        [SystemRoles.Security] = [Permission.ViewInventory, Permission.ManageExpirationPolicies, Permission.ViewAudit,
            Permission.VerifyAuditChain, Permission.ViewAlerts, Permission.ApproveCriticalAccess],
    };

    public static IReadOnlySet<Permission> For(IEnumerable<string> roles)
    {
        var set = new HashSet<Permission>();
        foreach (var role in roles)
        {
            if (ByRole.TryGetValue(role, out var perms))
                set.UnionWith(perms);
        }
        return set;
    }

    public static bool Has(this ICurrentUser user, Permission permission) => For(user.Roles).Contains(permission);

    public static void Require(this ICurrentUser user, Permission permission)
    {
        if (!user.Has(permission))
            throw new ForbiddenFailure();
    }
}
