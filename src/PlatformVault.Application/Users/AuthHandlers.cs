using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;

namespace PlatformVault.Application.Users;

public sealed record LoginCommand(string UserName, string Password, string? UserAgent);

public sealed record LogoutCommand;

public sealed record ReauthenticateCommand(string Password);

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed record GetMeQuery;

public sealed record MeView(UserView User, IReadOnlyList<string> Permissions, DateTime? LastReauthenticatedAt);

/// <summary>Inicio de sesión local con usuario y contraseña (Fase 1, IMP-17). Bloqueo tras 5 intentos (IMP-25).</summary>
public sealed class LoginHandler(IIdentityService identity, IRequestContext request, AuditLogger audit) : ICommandHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var userName = command.UserName?.Trim() ?? string.Empty;
        if (userName.Length is 0 or > 100 || string.IsNullOrEmpty(command.Password) || command.Password.Length > 256)
            throw new AuthenticationFailure();

        var result = await identity.PasswordSignInAsync(userName, command.Password, request.ClientIp, command.UserAgent, ct);
        var succeeded = result.Status == LoginStatus.Succeeded;
        await audit.RecordForAsync(ActorTypes.User, result.UserId?.ToString(), userName,
            succeeded ? AuditActions.Login : AuditActions.LoginFailed, "User", result.UserId?.ToString(),
            succeeded ? AuditResults.Success : AuditResults.Denied, new { status = result.Status.ToString() }, ct);

        return result.Status switch
        {
            LoginStatus.Succeeded => result,
            LoginStatus.LockedOut => throw new AuthenticationFailure("La cuenta está bloqueada temporalmente por intentos fallidos."),
            _ => throw new AuthenticationFailure(),
        };
    }
}

public sealed class LogoutHandler(ICurrentUser user, ISessionService sessions, AuditLogger audit) : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<Unit> HandleAsync(LogoutCommand command, CancellationToken ct)
    {
        await sessions.RevokeAsync(user.SessionId, "Logout", ct);
        await audit.SuccessAsync(AuditActions.Logout, "User", user.UserId.ToString(), null, ct);
        return Unit.Value;
    }
}

/// <summary>IMP-29: la contraseña reintroducida habilita revelar, descargar y aprobar durante 15 minutos.</summary>
public sealed class ReauthenticateHandler(ICurrentUser user, IIdentityService identity, AuditLogger audit) : ICommandHandler<ReauthenticateCommand, Unit>
{
    public async Task<Unit> HandleAsync(ReauthenticateCommand command, CancellationToken ct)
    {
        var ok = !string.IsNullOrEmpty(command.Password) && command.Password.Length <= 256
            && await identity.ReauthenticateAsync(user.SessionId, user.UserId, command.Password, ct);
        await audit.RecordAsync(AuditActions.Reauthenticate, "User", user.UserId.ToString(), ok ? AuditResults.Success : AuditResults.Denied, null, ct);
        return ok ? Unit.Value : throw new AuthenticationFailure("Contraseña incorrecta.");
    }
}

/// <summary>Cambio de contraseña: política de IMP-25 e historial de 4. Cierra las demás sesiones.</summary>
public sealed class ChangePasswordHandler(ICurrentUser user, IIdentityService identity, ISessionService sessions, AuditLogger audit)
    : ICommandHandler<ChangePasswordCommand, Unit>
{
    public async Task<Unit> HandleAsync(ChangePasswordCommand command, CancellationToken ct)
    {
        var errors = await identity.ChangePasswordAsync(user.UserId, command.CurrentPassword ?? string.Empty, command.NewPassword ?? string.Empty, ct);
        if (errors.Count > 0)
        {
            await audit.DeniedAsync(AuditActions.PasswordChanged, "User", user.UserId.ToString(), new { errors = errors.Count }, ct);
            throw new ValidationFailure("La contraseña no cumple la política.", new Dictionary<string, string[]> { ["newPassword"] = [.. errors] });
        }
        await sessions.RevokeAllForUserAsync(user.UserId, "PasswordChanged", user.SessionId, ct);
        await audit.SuccessAsync(AuditActions.PasswordChanged, "User", user.UserId.ToString(), null, ct);
        return Unit.Value;
    }
}

/// <summary>GET /me/effective-permissions: la Web solo lo usa para ocultar opciones; la API vuelve a autorizar cada acción.</summary>
public sealed class GetMeHandler(ICurrentUser user, IUserDirectory users) : IQueryHandler<GetMeQuery, MeView>
{
    public async Task<MeView> HandleAsync(GetMeQuery query, CancellationToken ct)
    {
        var view = await users.GetAsync(user.UserId, ct) ?? throw new NotFoundFailure("usuario");
        var permissions = Permissions.For(view.Roles).Select(p => p.ToString()).Order(StringComparer.Ordinal).ToList();
        return new MeView(view, permissions, user.LastReauthenticatedUtc);
    }
}
