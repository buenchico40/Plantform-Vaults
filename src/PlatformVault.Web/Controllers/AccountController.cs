using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformVault.Web.Api;

namespace PlatformVault.Web.Controllers;

public sealed class LoginForm
{
    [Required(ErrorMessage = "Indique el usuario."), MaxLength(100)]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Indique la contraseña."), MaxLength(256), DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public sealed class ChangePasswordForm
{
    [Required, MaxLength(256), DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required, MinLength(15, ErrorMessage = "Mínimo 15 caracteres."), MaxLength(256), DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden."), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class PasswordBody
{
    [Required, MaxLength(256)]
    public string Password { get; set; } = "";
}

public sealed class AccountController(PlatformApi api) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login() => View(new LoginForm());

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(form);
        LoginResponse login;
        try
        {
            login = await api.PostAsync<LoginResponse>("auth/login", new { userName = form.UserName, password = form.Password }, ct);
        }
        catch (ApiException ex) when (ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.TooManyRequests or HttpStatusCode.BadRequest)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            form.Password = string.Empty;
            return View(form);
        }
        await SignInAsync(login.SessionToken, login.MustChangePassword, login.ExpiresAt, ct);
        return login.MustChangePassword ? RedirectToAction(nameof(ChangePassword)) : RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordForm());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(new ChangePasswordForm());
        try
        {
            await api.SendAsync(HttpMethod.Post, "auth/change-password", new { currentPassword = form.CurrentPassword, newPassword = form.NewPassword }, ct);
        }
        catch (ApiException ex) when (ex.Status is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.UnprocessableEntity)
        {
            ModelState.AddModelError(string.Empty, Infrastructure.ApiExceptionFilter.Describe(ex));
            return View(new ChangePasswordForm());
        }
        // El cambio revoca las demás sesiones; se vuelve a iniciar sesión con la nueva contraseña.
        var userName = User.Identity?.Name ?? string.Empty;
        await api.SendAsync(HttpMethod.Post, "auth/logout", null, ct);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var login = await api.PostAsync<LoginResponse>("auth/login", new { userName, password = form.NewPassword }, ct);
        await SignInAsync(login.SessionToken, login.MustChangePassword, login.ExpiresAt, ct);
        TempData["Info"] = "Contraseña actualizada.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        try
        {
            await api.SendAsync(HttpMethod.Post, "auth/logout", null, ct);
        }
        catch (ApiException)
        {
            // La sesión ya no era válida en la API; se cierra igualmente en la Web.
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    /// <summary>IMP-29: re-autenticación con contraseña (AJAX) antes de revelar, descargar o aprobar.</summary>
    [HttpPost]
    public async Task<IActionResult> Reauthenticate([FromBody] PasswordBody body, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Post, "auth/reauthenticate", new { password = body.Password }, ct);
        return NoContent();
    }

    private async Task SignInAsync(string token, bool mustChange, DateTime expiresAt, CancellationToken ct)
    {
        var claims = new List<Claim> { new(WebClaims.SessionToken, token), new(WebClaims.MustChangePassword, mustChange ? "true" : "false") };
        // Se consulta el perfil con el token recién emitido para conocer roles y permisos (solo para mostrar u ocultar opciones).
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "pending"));
        var me = await api.GetAsync<MeView>("me/effective-permissions", ct);
        claims.Add(new Claim(ClaimTypes.Name, me.User.UserName));
        claims.Add(new Claim(ClaimTypes.GivenName, me.User.DisplayName));
        claims.Add(new Claim(WebClaims.UserId, me.User.Id.ToString()));
        claims.AddRange(me.User.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(me.Permissions.Select(p => new Claim(WebClaims.Permission, p)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { ExpiresUtc = expiresAt, IsPersistent = false });
    }
}
