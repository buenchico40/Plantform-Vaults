namespace PlatformVault.Application.Users;

/// <summary>Usuario para administración y para validar reglas (roles, estado). Sin datos de credenciales.</summary>
public sealed record UserView(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Email,
    Guid? AreaId,
    Guid? ManagerUserId,
    bool IsActive,
    bool IsLockedOut,
    bool MustChangePassword,
    DateTime? PasswordChangedAt,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles);

public sealed record UserLookup(Guid Id, string UserName, string DisplayName);

public sealed record NewUser(string UserName, string DisplayName, string? Email, Guid? AreaId, Guid? ManagerUserId);

public sealed record UserUpdate(string DisplayName, string? Email, Guid? AreaId, Guid? ManagerUserId, bool IsActive);

public sealed record AreaView(Guid Id, string Code, string Name, bool IsActive);

/// <summary>Resultado del inicio de sesión. El token solo viaja en esta respuesta; la base guarda su hash (IMP-26).</summary>
public sealed record LoginResult(LoginStatus Status, string? SessionToken = null, DateTime? ExpiresAtUtc = null, bool MustChangePassword = false,
    Guid? UserId = null);

public enum LoginStatus
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
}

/// <summary>Sesión validada (IMP-06).</summary>
public sealed record SessionPrincipal(
    Guid SessionId,
    Guid UserId,
    string UserName,
    string DisplayName,
    Guid? AreaId,
    bool MustChangePassword,
    DateTime? LastReauthUtc,
    DateTime ExpiresAtUtc,
    IReadOnlyList<string> Roles);

public sealed record DashboardSummary(
    int Total,
    int Active,
    int Expired,
    int ExpiringSoon,
    int WithoutOwner,
    int OpenAlerts,
    int PendingRequests,
    IReadOnlyList<CountByKey> ByType,
    IReadOnlyList<CountByKey> ByCriticality,
    IReadOnlyList<CountByKey> BySensitivity,
    IReadOnlyList<CountByKey> ByState,
    IReadOnlyList<CountByKey> ByExpirationStatus);

public sealed record CountByKey(string Key, int Count);

public sealed record JobRunView(long Id, string JobName, DateTime StartedAt, DateTime? FinishedAt, string Status, int ItemsProcessed, string? Detail);
