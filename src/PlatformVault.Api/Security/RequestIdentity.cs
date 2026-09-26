using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Users;

namespace PlatformVault.Api.Security;

/// <summary>Usuario y datos técnicos de la petición en curso (servicio con ámbito de petición).</summary>
public sealed class RequestIdentity : ICurrentUser, IRequestContext
{
    private SessionPrincipal? _session;

    public bool IsAuthenticated => _session is not null;
    public Guid UserId => _session?.UserId ?? Guid.Empty;
    public string UserName => _session?.UserName ?? string.Empty;
    public string DisplayName => _session?.DisplayName ?? string.Empty;
    public Guid? AreaId => _session?.AreaId;
    public Guid SessionId => _session?.SessionId ?? Guid.Empty;
    public DateTime? LastReauthenticatedUtc => _session?.LastReauthUtc;
    public IReadOnlyCollection<string> Roles => _session?.Roles ?? [];
    public bool MustChangePassword => _session?.MustChangePassword ?? false;

    public string? ClientIp { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public string Channel { get; set; } = "UI";

    public void SignIn(SessionPrincipal session) => _session = session;
}
