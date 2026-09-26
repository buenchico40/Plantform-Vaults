using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlatformVault.Web.Api;

public sealed class ApiOptions
{
    public const string Section = "Api";

    public Uri BaseUrl { get; set; } = new("https://localhost:5081/");

    /// <summary>
    /// API Key del canal Web → API (IMP-27). Solo vive en la configuración del servidor Web (user-secrets en desarrollo,
    /// variable de entorno protegida con DPAPI en servidores). Nunca se envía al navegador.
    /// </summary>
    public string Key { get; set; } = string.Empty;
}

/// <summary>Error devuelto por la API como ProblemDetails.</summary>
public sealed class ApiException(HttpStatusCode status, string code, string message, IReadOnlyDictionary<string, string[]>? errors, string? correlationId)
    : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
    public string? CorrelationId { get; } = correlationId;

    public bool RequiresReauthentication => Code == "REAUTHENTICATION_REQUIRED";
    public bool SessionExpired => Status == HttpStatusCode.Unauthorized && Code is "SESSION_INVALID" or "INVALID_CREDENTIALS" && !RequiresReauthentication;
}

/// <summary>Cliente HTTP de PlatformVault.Api. La Web no accede a la base de datos ni a la criptografía (prompt §2).</summary>
public sealed class PlatformApi(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<T> GetAsync<T>(string path, CancellationToken ct) => SendAsync<T>(HttpMethod.Get, path, null, null, ct);

    public Task<T> PostAsync<T>(string path, object? body, CancellationToken ct, string? ifMatch = null) =>
        SendAsync<T>(HttpMethod.Post, path, body, ifMatch, ct);

    public Task<T> PutAsync<T>(string path, object? body, string? ifMatch, CancellationToken ct) => SendAsync<T>(HttpMethod.Put, path, body, ifMatch, ct);

    public Task<T> PatchAsync<T>(string path, object? body, string? ifMatch, CancellationToken ct) => SendAsync<T>(HttpMethod.Patch, path, body, ifMatch, ct);

    public async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken ct, string? ifMatch = null)
    {
        using var response = await http.SendAsync(Build(method, path, body, ifMatch), ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> DownloadAsync(string path, object body, CancellationToken ct)
    {
        using var response = await http.SendAsync(Build(HttpMethod.Post, path, body, null), ct);
        await EnsureSuccessAsync(response, ct);
        var content = await response.Content.ReadAsByteArrayAsync(ct);
        var name = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? "archivo";
        return (content, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", name.Trim('"'));
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, string? ifMatch, CancellationToken ct)
    {
        using var response = await http.SendAsync(Build(method, path, body, ifMatch), ct);
        await EnsureSuccessAsync(response, ct);
        if (response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
            return default!;
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    private static HttpRequestMessage Build(HttpMethod method, string path, object? body, string? ifMatch)
    {
        var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        if (!string.IsNullOrEmpty(ifMatch))
            request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{ifMatch.Trim('"')}\""));
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        ProblemBody? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemBody>(Json, ct);
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }
        var message = problem?.Title ?? (response.StatusCode == HttpStatusCode.TooManyRequests
            ? "Demasiados intentos. Espere un minuto."
            : "No se pudo completar la operación.");
        throw new ApiException(response.StatusCode, problem?.Code ?? response.StatusCode.ToString(), message, problem?.Errors, problem?.CorrelationId);
    }

    private sealed class ProblemBody
    {
        public string? Title { get; set; }
        public string? Code { get; set; }
        public string? CorrelationId { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}

/// <summary>Añade la API Key, el token de sesión del usuario (desde la cookie cifrada) y el Correlation ID.</summary>
public sealed class ApiCredentialsHandler(IHttpContextAccessor accessor, Microsoft.Extensions.Options.IOptions<ApiOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-API-Key", options.Value.Key);
        var context = accessor.HttpContext;
        var token = context?.User.FindFirst(WebClaims.SessionToken)?.Value;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Add("X-User-Session", token);
        if (context is not null)
        {
            request.Headers.Add("X-Correlation-Id", WebClaims.CorrelationId(context));
            // IP del navegador para la auditoría (RN-072) y el límite de intentos por usuario final en la API.
            if (context.Connection.RemoteIpAddress is { } ip)
                request.Headers.Add("X-Client-Ip", ip.ToString());
        }
        return base.SendAsync(request, cancellationToken);
    }
}

public static class WebClaims
{
    public const string SessionToken = "pv:st";
    public const string Permission = "pv:perm";
    public const string MustChangePassword = "pv:mcp";
    public const string UserId = "pv:uid";

    public static string CorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue("pv:corr", out var value) && value is string s)
            return s;
        var id = Guid.NewGuid().ToString();
        context.Items["pv:corr"] = id;
        return id;
    }
}
