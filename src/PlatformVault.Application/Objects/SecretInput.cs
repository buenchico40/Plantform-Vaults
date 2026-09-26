using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PlatformVault.Application.Abstractions;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Objects;

/// <summary>
/// Valor sensible recibido para custodiar. Solo vive en memoria durante la petición; el llamador invoca
/// <see cref="Clear"/> al terminar.
/// </summary>
public sealed class SecretInput
{
    public const int MaxBytes = 64 * 1024;

    public string? Text { get; init; }
    public byte[]? KeyMaterial { get; init; }
    public byte[]? CertificateFile { get; init; }
    public string? CertificatePassword { get; init; }

    public bool IsEmpty => string.IsNullOrEmpty(Text) && KeyMaterial is not { Length: > 0 } && CertificateFile is not { Length: > 0 };

    public void Clear()
    {
        if (KeyMaterial is not null) CryptographicOperations.ZeroMemory(KeyMaterial);
        if (CertificateFile is not null) CryptographicOperations.ZeroMemory(CertificateFile);
    }
}

/// <summary>Valor preparado para cifrar y, en certificados, los metadatos extraídos del archivo.</summary>
internal sealed record PreparedPayload(byte[]? Bytes, CertificateInfo? Certificate)
{
    public void Clear()
    {
        if (Bytes is not null) CryptographicOperations.ZeroMemory(Bytes);
    }
}

internal static class PayloadPreparation
{
    /// <summary>Convierte la entrada al formato custodiado según la forma de valor del subtipo (IMP-30).</summary>
    public static PreparedPayload Prepare(SecretInput? input, ObjectType type, PayloadKind? kind, ICertificateInspector inspector)
    {
        if (input is null || input.IsEmpty)
            return new PreparedPayload(null, null);

        if (type == ObjectType.Certificate && input.CertificateFile is { Length: > 0 } file)
        {
            if (file.Length > SecretInput.MaxBytes)
                throw new DomainException(DomainErrors.InvalidValue, "El archivo supera el tamaño máximo de 64 KB.");
            var (info, pkcs12) = inspector.Inspect(file, input.CertificatePassword);
            return new PreparedPayload(pkcs12, info);
        }

        if (input.CertificateFile is { Length: > 0 })
            throw new DomainException(DomainErrors.PayloadNotAllowed, "Solo los certificados admiten archivo de certificado.");

        switch (kind)
        {
            case PayloadKind.Text when !string.IsNullOrEmpty(input.Text) && input.KeyMaterial is null:
                var bytes = Encoding.UTF8.GetBytes(input.Text);
                if (bytes.Length > SecretInput.MaxBytes)
                {
                    CryptographicOperations.ZeroMemory(bytes);
                    throw new DomainException(DomainErrors.InvalidValue, "El valor supera el tamaño máximo de 64 KB.");
                }
                return new PreparedPayload(bytes, null);
            case PayloadKind.KeyMaterial when input.KeyMaterial is { Length: > 0 } key && string.IsNullOrEmpty(input.Text):
                if (key.Length > SecretInput.MaxBytes)
                    throw new DomainException(DomainErrors.InvalidValue, "El material de clave supera el tamaño máximo de 64 KB.");
                return new PreparedPayload(key.ToArray(), null);
            default:
                throw new DomainException(DomainErrors.PayloadNotAllowed, "El formato del valor no corresponde al subtipo del objeto.");
        }
    }

    /// <summary>Atributos del certificado que se guardan como metadatos (RN-016).</summary>
    public static Dictionary<string, string> CertificateAttributes(CertificateInfo info) => new(StringComparer.Ordinal)
    {
        ["subject"] = Truncate(info.Subject),
        ["issuer"] = Truncate(info.Issuer),
        ["serialNumber"] = info.SerialNumber,
        ["subjectAlternativeNames"] = Truncate(string.Join(", ", info.SubjectAlternativeNames)),
        ["notBefore"] = info.NotBeforeUtc.ToString("O", CultureInfo.InvariantCulture),
        ["notAfter"] = info.NotAfterUtc.ToString("O", CultureInfo.InvariantCulture),
        ["signatureAlgorithm"] = info.SignatureAlgorithm,
        ["keyAlgorithm"] = info.KeyAlgorithm,
        ["keySize"] = info.KeySize.ToString(CultureInfo.InvariantCulture),
        ["hasPrivateKey"] = info.HasPrivateKey ? "true" : "false",
    };

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}
