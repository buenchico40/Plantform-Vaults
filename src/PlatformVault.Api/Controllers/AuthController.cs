using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Users;

namespace PlatformVault.Api.Controllers;

public sealed record LoginRequest([Required, MaxLength(100)] string UserName, [Required, MaxLength(256)] string Password);

public sealed record LoginResponse(string SessionToken, DateTime ExpiresAt, bool MustChangePassword);

public sealed record ReauthenticateRequest([Required, MaxLength(256)] string Password);

public sealed record ChangePasswordRequest([Required, MaxLength(256)] string CurrentPassword, [Required, MaxLength(256)] string NewPassword);

/// <summary>Autenticación local de la Fase 1 (usuario y contraseña, IMP-17). Solo la invoca PlatformVault.Web.</summary>
[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request,
        [FromServices] ICommandHandler<LoginCommand, LoginResult> handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.UserName, request.Password, Request.Headers.UserAgent.ToString()), ct);
        return new LoginResponse(result.SessionToken!, result.ExpiresAtUtc!.Value, result.MustChangePassword);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromServices] ICommandHandler<LogoutCommand, Unit> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new LogoutCommand(), ct);
        return NoContent();
    }

    /// <summary>IMP-29: habilita revelar, descargar y aprobar durante 15 minutos.</summary>
    [HttpPost("reauthenticate")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Reauthenticate(ReauthenticateRequest request,
        [FromServices] ICommandHandler<ReauthenticateCommand, Unit> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new ReauthenticateCommand(request.Password), ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request,
        [FromServices] ICommandHandler<ChangePasswordCommand, Unit> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }
}

[Route("api/v1/me")]
public sealed class MeController : ApiControllerBase
{
    [HttpGet("effective-permissions")]
    public Task<MeView> Get([FromServices] IQueryHandler<GetMeQuery, MeView> handler, CancellationToken ct) =>
        handler.HandleAsync(new GetMeQuery(), ct);
}
