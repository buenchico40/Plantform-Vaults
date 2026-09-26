using PlatformVault.Domain.Common;

namespace PlatformVault.Domain.Objects;

/// <summary>Subtipo del catálogo (RN-002) con las reglas que dependen de él.</summary>
public sealed record SubtypeDefinition(
    ObjectType Type,
    string Code,
    string DisplayName,
    PayloadKind? PayloadKind,
    Sensitivity MinimumSensitivityWithPayload,
    Criticality? RequiredCriticality,
    IReadOnlyList<string> RequiredDetails);

/// <summary>Catálogo de tipos y subtipos del documento de visión §1.2 (IMP-10).</summary>
public static class ObjectCatalog
{
    public const string DetailTargetSystem = "targetSystem";
    public const string DetailAccountName = "accountName";
    public const string DetailAccountIdentifier = "accountIdentifier";
    public const string DetailDirectorySource = "directorySource";

    private static readonly string[] None = [];
    private static readonly string[] CredentialDetails = [DetailTargetSystem, DetailAccountName];
    private static readonly string[] ServiceAccountDetails = [DetailDirectorySource, DetailAccountIdentifier];

    private static readonly SubtypeDefinition[] All =
    [
        Cert("Digital", "Certificado digital"),
        Cert("SslTls", "SSL/TLS"),
        Cert("Vpn", "VPN"),
        Cert("Api", "API (mTLS)"),
        // RN-019: los certificados SWIFT son Críticos; rebajarlos exige aprobación de Seguridad (iteración 1B).
        new(ObjectType.Certificate, "Swift", "SWIFT", PayloadKind.Pkcs12, Sensitivity.Restricted, Criticality.Critical, None),
        Cert("Signing", "Firma"),
        new(ObjectType.CryptographicKey, "Symmetric", "Llave simétrica", PayloadKind.KeyMaterial, Sensitivity.Restricted, null, None),
        new(ObjectType.CryptographicKey, "Asymmetric", "Llave asimétrica", PayloadKind.KeyMaterial, Sensitivity.Restricted, null, None),
        Text(ObjectType.Secret, "ApplicationSecret", "Secreto de aplicación", None),
        Text(ObjectType.Secret, "ApiKey", "API Key", None),
        Text(ObjectType.Secret, "OAuthToken", "OAuth Token", None),
        Text(ObjectType.Credential, "TechnicalPassword", "Contraseña técnica", CredentialDetails),
        Text(ObjectType.Credential, "Database", "Base de datos", CredentialDetails),
        Text(ObjectType.Credential, "Infrastructure", "Infraestructura", CredentialDetails),
        Text(ObjectType.Credential, "NetworkDevice", "Dispositivo de red", CredentialDetails),
        Text(ObjectType.ServiceAccount, "ServicePrincipal", "Service Principal", ServiceAccountDetails),
        Text(ObjectType.ServiceAccount, "ManagedIdentity", "Managed Identity", ServiceAccountDetails),
        Text(ObjectType.ServiceAccount, "DirectoryAccount", "Cuenta de servicio de directorio o local", ServiceAccountDetails),
        Text(ObjectType.ServiceAccount, "DatabaseAccount", "Cuenta de servicio de base de datos", ServiceAccountDetails),
    ];

    public static IReadOnlyList<SubtypeDefinition> Subtypes => All;

    public static SubtypeDefinition Get(ObjectType type, string? subtype)
    {
        var match = All.FirstOrDefault(s => s.Type == type && string.Equals(s.Code, subtype, StringComparison.Ordinal));
        return match ?? throw new DomainException(DomainErrors.InvalidValue, $"El subtipo '{subtype}' no pertenece al catálogo del tipo {type}.");
    }

    // RN-004 y RN-017: la clave privada de un certificado es Restringida.
    private static SubtypeDefinition Cert(string code, string name) =>
        new(ObjectType.Certificate, code, name, PayloadKind.Pkcs12, Sensitivity.Restricted, null, None);

    private static SubtypeDefinition Text(ObjectType type, string code, string name, string[] details) =>
        new(type, code, name, PayloadKind.Text, Sensitivity.Confidential, null, details);
}
