using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PlatformVault.Web.Api;

namespace PlatformVault.Web.Infrastructure;

/// <summary>Cabeceras de seguridad y CSP con nonce por petición (sin scripts en línea no autorizados).</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string NonceKey = "pv:nonce";

    public Task InvokeAsync(HttpContext context)
    {
        var nonce = Program.NewNonce();
        context.Items[NonceKey] = nonce;
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy =
            $"default-src 'self'; script-src 'self' 'nonce-{nonce}'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
            "font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'";
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers.CacheControl = "no-store";
        return next(context);
    }
}

/// <summary>Con contraseña temporal o caducada solo se permite cambiarla (IMP-25).</summary>
public sealed class PasswordChangeGateMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (context.User.Identity?.IsAuthenticated == true && context.User.HasClaim(WebClaims.MustChangePassword, "true")
            && !path.StartsWithSegments("/Account") && !path.StartsWithSegments("/lib") && !path.StartsWithSegments("/css")
            && !path.StartsWithSegments("/js"))
        {
            context.Response.Redirect("/Account/ChangePassword");
            return Task.CompletedTask;
        }
        return next(context);
    }
}

/// <summary>
/// Traduce los errores de la API: sesión vencida → nuevo inicio de sesión; errores de negocio → mensaje en pantalla.
/// Las peticiones AJAX reciben JSON con el código para que el script pida re-autenticación si hace falta.
/// </summary>
public sealed class ApiExceptionFilter(ITempDataDictionaryFactory tempDataFactory) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ApiException ex)
            return;
        var http = context.HttpContext;
        var isAjax = http.Request.Headers.XRequestedWith == "XMLHttpRequest";

        if (ex.Status == HttpStatusCode.Unauthorized && !ex.RequiresReauthentication && ex.Code == "SESSION_INVALID")
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Result = isAjax
                ? new JsonResult(new { code = ex.Code, message = "La sesión expiró." }) { StatusCode = 401 }
                : new RedirectResult("/Account/Login");
            context.ExceptionHandled = true;
            return;
        }

        if (isAjax)
        {
            context.Result = new JsonResult(new { code = ex.Code, message = ex.Message, errors = ex.Errors, correlationId = ex.CorrelationId })
            {
                StatusCode = (int)ex.Status,
            };
            context.ExceptionHandled = true;
            return;
        }

        var tempData = tempDataFactory.GetTempData(http);
        tempData["Error"] = Describe(ex);
        var referer = http.Request.Headers.Referer.ToString();
        context.Result = ex.Status == HttpStatusCode.NotFound
            ? new ViewResult { ViewName = "~/Views/Shared/NotFound.cshtml" }
            : new RedirectResult(Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == http.Request.Host.Host ? uri.PathAndQuery : "/");
        context.ExceptionHandled = true;
    }

    public static string Describe(ApiException ex)
    {
        var details = ex.Errors.SelectMany(e => e.Value).ToList();
        var text = details.Count > 0 ? $"{ex.Message} {string.Join(" ", details)}" : ex.Message;
        return ex.CorrelationId is null ? text : $"{text} (referencia {ex.CorrelationId})";
    }
}

public static class UserExtensions
{
    public static bool Can(this ClaimsPrincipal user, string permission) => user.HasClaim(WebClaims.Permission, permission);

    public static bool IsRole(this ClaimsPrincipal user, string role) => user.IsInRole(role);

    public static Guid Id(this ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(WebClaims.UserId), out var id) ? id : Guid.Empty;
}

public static class Display
{
    public static string Type(string code) => code switch
    {
        "Certificate" => "Certificado",
        "CryptographicKey" => "Clave criptográfica",
        "Secret" => "Secreto",
        "Credential" => "Credencial",
        "ServiceAccount" => "Cuenta de servicio",
        "Critical" => "Crítico",
        "High" => "Alto",
        "Medium" => "Medio",
        "Low" => "Bajo",
        "Public" => "Pública",
        "Internal" => "Interna",
        "Confidential" => "Confidencial",
        "Restricted" => "Restringida",
        "Draft" => "Borrador",
        "Active" => "Activo",
        "Suspended" => "Suspendido",
        "Deactivated" => "Desactivado",
        "Valid" => "Vigente",
        "ExpiringSoon" => "Próximo a vencer",
        "Expired" => "Expirado",
        "NoExpiration" => "Sin vencimiento",
        "Reveal" => "Revelar",
        "DownloadPrivateKey" => "Descargar llave privada",
        "DownloadKeyMaterial" => "Descargar material de clave",
        "Security" => "Seguridad",
        "GroupPeer" => "Par del grupo",
        "Owner" => "Propietario",
        _ => code,
    };

    public static string Date(DateTime? value) => value is { } v ? v.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "—";
}

public static class Csp
{
    public static string Nonce(HttpContext context) => context.Items[SecurityHeadersMiddleware.NonceKey] as string ?? string.Empty;
}

