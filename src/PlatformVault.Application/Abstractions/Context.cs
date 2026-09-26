using PlatformVault.Domain.Identity;

namespace PlatformVault.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>Usuario de la sesión en curso, resuelto por la API a partir del token opaco (IMP-06).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string UserName { get; }
    string DisplayName { get; }
    Guid? AreaId { get; }
    Guid SessionId { get; }
    DateTime? LastReauthenticatedUtc { get; }
    IReadOnlyCollection<string> Roles { get; }
}

/// <summary>Datos técnicos de la petición para auditoría (RN-072).</summary>
public interface IRequestContext
{
    string? ClientIp { get; }
    Guid CorrelationId { get; }
    string Channel { get; }
}

public static class CurrentUserExtensions
{
    public static readonly TimeSpan ReauthenticationWindow = TimeSpan.FromMinutes(15);

    public static bool IsInRole(this ICurrentUser user, string role) => user.Roles.Contains(role, StringComparer.Ordinal);

    public static bool HasGlobalScope(this ICurrentUser user) => SystemRoles.GlobalScope.Any(user.IsInRole);

    /// <summary>Ámbito de visibilidad que aplica la función app.ufn_VisibleObjects (RN-010).</summary>
    public static VisibilityScope Scope(this ICurrentUser user) => new(user.UserId, user.HasGlobalScope());

    /// <summary>IMP-29: revelar, descargar y aprobar exigen contraseña reintroducida hace 15 minutos o menos.</summary>
    public static void EnsureRecentlyReauthenticated(this ICurrentUser user, DateTime nowUtc)
    {
        if (user.LastReauthenticatedUtc is not { } last || nowUtc - last > ReauthenticationWindow)
            throw new ReauthenticationRequiredFailure();
    }
}

public sealed record VisibilityScope(Guid ViewerUserId, bool HasGlobalScope);
