using PlatformVault.Application.Abstractions;

namespace PlatformVault.Api.Security;

/// <summary>
/// Resuelve el usuario a partir del token opaco de la cabecera X-User-Session (IMP-06). Las rutas anónimas son solo
/// el inicio de sesión y la salud. Con contraseña caducada o temporal solo se permite cambiarla (IMP-25).
/// </summary>
public sealed class SessionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-User-Session";

    private static readonly string[] Anonymous = ["/api/v1/auth/login", "/health"];
    private static readonly string[] AllowedWithPasswordChange =
        ["/api/v1/auth/change-password", "/api/v1/auth/logout", "/api/v1/me/effective-permissions"];

    public async Task InvokeAsync(HttpContext context, RequestIdentity identity, ISessionService sessions)
    {
        var path = context.Request.Path;
        if (Anonymous.Any(a => path.StartsWithSegments(a, StringComparison.OrdinalIgnoreCase))
            || Infrastructure.ApiDocumentation.IsDocumentationRequest(context))
        {
            await next(context);
            return;
        }

        var token = context.Request.Headers[HeaderName].ToString();
        var session = string.IsNullOrEmpty(token) || token.Length > 100 ? null : await sessions.ValidateAsync(token, context.RequestAborted);
        if (session is null)
        {
            await WriteAsync(context, identity, StatusCodes.Status401Unauthorized, "SESSION_INVALID", "La sesión no es válida o expiró.");
            return;
        }
        identity.SignIn(session);

        if (session.MustChangePassword && !AllowedWithPasswordChange.Any(a => path.StartsWithSegments(a, StringComparison.OrdinalIgnoreCase)))
        {
            await WriteAsync(context, identity, StatusCodes.Status403Forbidden, "PASSWORD_CHANGE_REQUIRED", "Debe cambiar su contraseña antes de continuar.");
            return;
        }
        await next(context);
    }

    private static Task WriteAsync(HttpContext context, RequestIdentity identity, int status, string code, string title)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { type = "about:blank", title, status, code, correlationId = identity.CorrelationId },
            context.RequestAborted);
    }
}

/// <summary>Correlation ID de la petición: se acepta el del cliente si es un GUID, se devuelve en la respuesta y va a la auditoría.</summary>
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, RequestIdentity identity)
    {
        if (Guid.TryParse(context.Request.Headers[HeaderName].ToString(), out var incoming))
            identity.CorrelationId = incoming;
        identity.ClientIp = context.Connection.RemoteIpAddress?.ToString();
        context.Response.Headers[HeaderName] = identity.CorrelationId.ToString();
        // Las respuestas de la API nunca se guardan en cachés intermedias ni del navegador.
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        using (context.RequestServices.GetRequiredService<ILogger<CorrelationMiddleware>>()
                   .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = identity.CorrelationId }))
        {
            await next(context);
        }
    }
}
