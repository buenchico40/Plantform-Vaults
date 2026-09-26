using System.Text.Json.Serialization;

namespace PlatformVault.Domain.Objects;

// Los nombres de los miembros son los códigos almacenados en la base de datos (IMP-02).
// JsonStringEnumMemberName fija el valor del contrato OpenAPI.

public enum ObjectType
{
    Certificate,
    CryptographicKey,
    Secret,
    Credential,
    ServiceAccount,
}

public enum Criticality
{
    [JsonStringEnumMemberName("Crítico")] Critical,
    [JsonStringEnumMemberName("Alto")] High,
    [JsonStringEnumMemberName("Medio")] Medium,
    [JsonStringEnumMemberName("Bajo")] Low,
}

public enum Sensitivity
{
    [JsonStringEnumMemberName("Pública")] Public,
    [JsonStringEnumMemberName("Interna")] Internal,
    [JsonStringEnumMemberName("Confidencial")] Confidential,
    [JsonStringEnumMemberName("Restringida")] Restricted,
}

public enum DeploymentEnvironment
{
    [JsonStringEnumMemberName("Producción")] Production,
    [JsonStringEnumMemberName("ContingenciaDR")] DisasterRecovery,
    [JsonStringEnumMemberName("PreproducciónUAT")] PreProduction,
    [JsonStringEnumMemberName("QA")] QA,
    [JsonStringEnumMemberName("Desarrollo")] Development,
}

public enum LifecycleState
{
    [JsonStringEnumMemberName("Borrador")] Draft,
    [JsonStringEnumMemberName("Activo")] Active,
    [JsonStringEnumMemberName("Suspendido")] Suspended,
    [JsonStringEnumMemberName("Desactivado")] Deactivated,
}

public enum ExpirationStatus
{
    [JsonStringEnumMemberName("Vigente")] Valid,
    [JsonStringEnumMemberName("PróximoAVencer")] ExpiringSoon,
    [JsonStringEnumMemberName("Expirado")] Expired,
    [JsonStringEnumMemberName("SinVencimiento")] NoExpiration,
}

public enum CustodyMode
{
    Internal,
    MetadataOnly,
}

/// <summary>
/// Rol registrado en el historial de propiedad. IMP-62: el objeto tiene un único propietario (<see cref="Owner"/>);
/// Functional y Technical solo aparecen en el historial anterior a la unificación.
/// </summary>
public enum OwnerRole
{
    [JsonStringEnumMemberName("Funcional")] Functional,
    [JsonStringEnumMemberName("Técnico")] Technical,
    [JsonStringEnumMemberName("Propietario")] Owner,
}

/// <summary>Forma del valor sensible custodiado (IMP-30).</summary>
public enum PayloadKind
{
    /// <summary>Texto: contraseña, secreto, API Key, token.</summary>
    Text,
    /// <summary>PKCS#12 con clave privada, re-exportado sin contraseña antes de cifrarse (RN-015).</summary>
    Pkcs12,
    /// <summary>Material de clave criptográfica (binario).</summary>
    KeyMaterial,
}
