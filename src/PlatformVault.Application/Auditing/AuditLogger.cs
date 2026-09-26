using System.Text.Json;
using PlatformVault.Application.Abstractions;

namespace PlatformVault.Application.Auditing;

/// <summary>
/// Registra eventos de auditoría con los datos de la petición. Si la escritura falla, la operación se rechaza
/// (fail-closed, RN-079). Los detalles nunca incluyen valores sensibles (RN-077).
/// </summary>
public sealed class AuditLogger(IAuditTrail trail, ICurrentUser user, IRequestContext request, IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<Guid> RecordAsync(string action, string? resourceType, string? resourceId, string result, object? details,
        CancellationToken ct) =>
        user.IsAuthenticated
            ? RecordForAsync(ActorTypes.User, user.UserId.ToString(), user.UserName, action, resourceType, resourceId, result, details, ct)
            : RecordForAsync(ActorTypes.System, null, null, action, resourceType, resourceId, result, details, ct);

    /// <summary>Evento con actor explícito (inicio de sesión, cuando aún no hay usuario autenticado).</summary>
    public async Task<Guid> RecordForAsync(string actorType, string? actorId, string? actorName, string action, string? resourceType,
        string? resourceId, string result, object? details, CancellationToken ct)
    {
        var entry = new AuditEntry(
            Guid.NewGuid(),
            clock.UtcNow,
            actorType,
            actorId,
            actorName,
            action,
            resourceType,
            resourceId,
            result,
            request.ClientIp,
            request.CorrelationId,
            Serialize(details));
        try
        {
            await trail.AppendAsync(entry, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new AuditUnavailableFailure(ex);
        }
        return entry.EventId;
    }

    public Task<Guid> SuccessAsync(string action, string? resourceType, string? resourceId, object? details, CancellationToken ct) =>
        RecordAsync(action, resourceType, resourceId, AuditResults.Success, details, ct);

    public Task<Guid> DeniedAsync(string action, string? resourceType, string? resourceId, object? details, CancellationToken ct) =>
        RecordAsync(action, resourceType, resourceId, AuditResults.Denied, details, ct);

    /// <summary>Evento de uso (RN-060, RN-072), sin valor sensible (RN-074).</summary>
    public async Task RecordUsageAsync(Guid objectId, string action, CancellationToken ct)
    {
        var entry = new UsageEntry(objectId, ActorTypes.User, user.UserId.ToString(), action, clock.UtcNow, request.Channel,
            AuditResults.Success, request.ClientIp, request.CorrelationId);
        try
        {
            await trail.AppendUsageAsync(entry, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new AuditUnavailableFailure(ex);
        }
    }

    internal static string? Serialize(object? details)
    {
        if (details is null) return null;
        var json = JsonSerializer.Serialize(details, JsonOptions);
        return json.Length <= 4000 ? json : json[..4000];
    }
}
