using System.Text.Json.Serialization;

namespace PlatformVault.Domain.Expiration;

public enum AlertSeverity
{
    [JsonStringEnumMemberName("Baja")] Low,
    [JsonStringEnumMemberName("Media")] Medium,
    [JsonStringEnumMemberName("Alta")] High,
    [JsonStringEnumMemberName("Crítica")] Critical,
}

public enum AlertState
{
    [JsonStringEnumMemberName("Generada")] Open,
    [JsonStringEnumMemberName("Reconocida")] Acknowledged,
    [JsonStringEnumMemberName("Escalada")] Escalated,
    [JsonStringEnumMemberName("Resuelta")] Resolved,
}

public enum AlertKind
{
    Threshold,
    Expired,
}
