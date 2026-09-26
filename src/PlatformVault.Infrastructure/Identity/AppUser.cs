namespace PlatformVault.Infrastructure.Identity;

/// <summary>Usuario local de ASP.NET Core Identity (IMP-04). Se persiste solo con procedimientos del esquema identity.</summary>
public sealed class AppUser
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
    public bool LockoutEnabled { get; set; } = true;
    public DateTimeOffset? LockoutEndUtc { get; set; }
    public int AccessFailedCount { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? AreaId { get; set; }
    public Guid? ManagerUserId { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }
    public bool MustChangePassword { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Actor de la operación en curso (CreatedBy / ModifiedBy / AssignedBy). No se lee de la base.</summary>
    internal Guid? ActingUserId { get; set; }

    /// <summary>La contraseña cambió en esta operación: se registra en el historial al guardar (IMP-25).</summary>
    internal bool PasswordChanged { get; set; }
}
