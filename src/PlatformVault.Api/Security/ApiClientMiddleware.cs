using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace PlatformVault.Api.Security;

public sealed class ApiClientOptions
{
    public const string Section = "ApiClients";
    public const string HeaderName = "X-API-Key";

    /// <summary>Clientes autorizados. En la Fase 1 el único cliente es PlatformVault.Web (IMP-05, IMP-27).</summary>
    public IList<ApiClient> Clients { get; } = [];
}

public sealed class ApiClient
{
    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 de la API Key en Base64. La API nunca guarda la clave en claro.</summary>
    public string KeySha256 { get; set; } = string.Empty;

    /// <summary>Redes permitidas en notación CIDR (IMP-28).</summary>
    public IList<string> AllowedNetworks { get; } = [];
}

/// <summary>
/// Autenticación del cliente Web → API antes de cualquier controlador: IP de origen dentro de una red permitida
/// y API Key válida comparada en tiempo constante (IMP-05). Todo lo demás responde 401 sin detalle.
/// </summary>
public sealed class ApiClientMiddleware(RequestDelegate next, IOptionsMonitor<ApiClientOptions> options, ILogger<ApiClientMiddleware> logger)
{
    public const string ClientIpHeader = "X-Client-Ip";

    public async Task InvokeAsync(HttpContext context, RequestIdentity identity)
    {
        if (context.Request.Path.StartsWithSegments("/health/live", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress;
        var key = context.Request.Headers[ApiClientOptions.HeaderName].ToString();
        var client = ip is null ? null : Match(options.CurrentValue, ip, key);
        if (client is null)
        {
            logger.LogWarning("Cliente de API rechazado desde {ClientIp}. CorrelationId {CorrelationId}", ip, identity.CorrelationId);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "about:blank",
                title = "Cliente no autorizado",
                status = 401,
                correlationId = identity.CorrelationId,
            }, context.RequestAborted);
            return;
        }

        identity.Channel = "UI";
        // La Web informa la IP del navegador. Solo se acepta de un cliente ya autenticado (API Key + red permitida) y se usa
        // para la auditoría (RN-072) y el límite de inicios de sesión por usuario final.
        if (IPAddress.TryParse(context.Request.Headers[ClientIpHeader].ToString(), out var endUserIp))
            identity.ClientIp = endUserIp.ToString();
        await next(context);
    }

    internal static ApiClient? Match(ApiClientOptions options, IPAddress ip, string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 512)
            return null;
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        var presented = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        foreach (var client in options.Clients)
        {
            if (!client.AllowedNetworks.Any(n => IPNetwork.TryParse(n, out var network) && network.Contains(ip)))
                continue;
            byte[] expected;
            try
            {
                expected = Convert.FromBase64String(client.KeySha256);
            }
            catch (FormatException)
            {
                continue;
            }
            if (expected.Length == presented.Length && CryptographicOperations.FixedTimeEquals(expected, presented))
                return client;
        }
        return null;
    }
}
