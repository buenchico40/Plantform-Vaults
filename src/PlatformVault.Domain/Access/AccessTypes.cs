using System.Text.Json.Serialization;

namespace PlatformVault.Domain.Access;

/// <summary>Acción sobre el valor sensible (contrato: AccessActionType).</summary>
public enum AccessAction
{
    Reveal,
    DownloadPrivateKey,
    DownloadKeyMaterial,
}

/// <summary>Quién aprueba la solicitud (RN-045).</summary>
public enum ApproverKind
{
    /// <summary>Objetos Críticos: rol Seguridad (RN-122).</summary>
    Security,
    /// <summary>Objetos Restringidos no críticos: par del mismo grupo (RN-106).</summary>
    GroupPeer,
    /// <summary>Objetos Confidenciales: propietario (RN-045 c).</summary>
    Owner,
}

public enum RequestState
{
    [JsonStringEnumMemberName("Pendiente")] Pending,
    [JsonStringEnumMemberName("Aprobada")] Approved,
    [JsonStringEnumMemberName("Rechazada")] Rejected,
    [JsonStringEnumMemberName("Cancelada")] Cancelled,
    [JsonStringEnumMemberName("Expirada")] Expired,
}

public enum Decision
{
    Approved,
    Rejected,
}

public enum TemporaryAccessState
{
    [JsonStringEnumMemberName("Programado")] Scheduled,
    [JsonStringEnumMemberName("Activo")] Active,
    [JsonStringEnumMemberName("Expirado")] Expired,
    [JsonStringEnumMemberName("Revocado")] Revoked,
}
