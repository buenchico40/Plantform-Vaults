using PlatformVault.Domain.Common;

namespace PlatformVault.Domain.Identity;

/// <summary>Roles de sistema (glosario §4.1). Los nombres coinciden con la semilla 07-SeedData/700-Roles.sql.</summary>
public static class SystemRoles
{
    public const string Administrator = "Administrador";
    public const string Custodian = "Custodio";
    public const string Owner = "Propietario";
    public const string Operator = "Operador";
    public const string Auditor = "Auditor";
    public const string Security = "Seguridad";

    public static readonly IReadOnlyList<string> All = [Administrator, Custodian, Owner, Operator, Auditor, Security];

    /// <summary>Roles que se asignan a mano. Propietario se deriva de la propiedad (RN-037).</summary>
    public static readonly IReadOnlyList<string> Assignable = [Administrator, Custodian, Operator, Auditor, Security];

    /// <summary>Roles incompatibles con cualquier otro (matriz §4.3, RN-036).</summary>
    public static readonly IReadOnlyList<string> Exclusive = [Auditor, Security];

    /// <summary>Roles con visibilidad global de metadatos (RN-010).</summary>
    public static readonly IReadOnlyList<string> GlobalScope = [Administrator, Auditor, Security];

    public static bool IsKnown(string role) => All.Contains(role, StringComparer.Ordinal);
}

/// <summary>Reglas de asignación de roles, pertenencia a grupos y propiedad (RN-036, RN-038, RN-103).</summary>
public static class SegregationOfDuties
{
    public static IReadOnlyList<string> ValidateRoleSet(Guid assignerId, Guid targetUserId, IEnumerable<string> roles)
    {
        var set = roles.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        foreach (var role in set)
        {
            if (!SystemRoles.IsKnown(role))
                throw new DomainException(DomainErrors.InvalidValue, $"Rol desconocido: {role}.");
            if (!SystemRoles.Assignable.Contains(role, StringComparer.Ordinal))
                throw new DomainException(DomainErrors.RoleConflict, $"El rol {role} no se asigna manualmente (RN-037).");
        }
        if (set.Count > 1 && set.Any(r => SystemRoles.Exclusive.Contains(r, StringComparer.Ordinal)))
            throw new DomainException(DomainErrors.RoleConflict, "Auditor y Seguridad son incompatibles con cualquier otro rol (RN-036).");
        if (assignerId == targetUserId)
            throw new DomainException(DomainErrors.SelfAssignment, "Nadie puede asignarse roles a sí mismo (RN-038).");
        return set;
    }

    public static void EnsureCanBeGroupMember(IEnumerable<string> roles, bool isActive)
    {
        if (!isActive)
            throw new DomainException(DomainErrors.MemberNotAllowed, "Un usuario inactivo no puede ser miembro de un grupo (RN-103).");
        if (roles.Any(r => SystemRoles.Exclusive.Contains(r, StringComparer.Ordinal)))
            throw new DomainException(DomainErrors.MemberNotAllowed, "Los usuarios con rol Auditor o Seguridad no pueden ser miembros de grupos (RN-103).");
    }

    public static void EnsureCanOwn(IEnumerable<string> roles, bool isActive)
    {
        if (!isActive)
            throw new DomainException(DomainErrors.OwnersRequired, "El propietario debe ser un usuario activo (RN-087).");
        if (roles.Any(r => SystemRoles.Exclusive.Contains(r, StringComparer.Ordinal)))
            throw new DomainException(DomainErrors.RoleConflict, "Los usuarios con rol Auditor o Seguridad no pueden ser propietarios (matriz §4.3).");
    }
}
