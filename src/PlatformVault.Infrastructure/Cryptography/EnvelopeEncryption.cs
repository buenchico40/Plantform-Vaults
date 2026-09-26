using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using PlatformVault.Application.Abstractions;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Infrastructure.Cryptography;

public sealed class KeyProtectionOptions
{
    public const string Section = "KeyProtection";

    /// <summary>LocalMachine en servidores (DEC-35); CurrentUser en desarrollo.</summary>
    public StoreLocation StoreLocation { get; set; } = StoreLocation.LocalMachine;

    /// <summary>Huella SHA-1 (la que muestra Windows) del certificado KEK vigente.</summary>
    public string ActiveThumbprint { get; set; } = string.Empty;

    /// <summary>KEK anteriores, solo para descifrar hasta completar la re-envoltura (IMP-31, rotación en 1B).</summary>
    public IList<string> PreviousThumbprints { get; } = [];

    public const int MinimumRsaKeySize = 3072;
}

/// <summary>
/// Cifrado de sobre (IMP-30): DEK AES-256-GCM aleatoria por objeto, versión y componente, envuelta con
/// RSA-OAEP-SHA256 con la llave privada no exportable del certificado KEK (DEC-35). Solo primitivas de .NET.
/// </summary>
public sealed class EnvelopeEncryption : IEnvelopeEncryption, IDisposable
{
    public const string AlgorithmName = "AES-256-GCM/RSA-OAEP-SHA256";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly X509Certificate2 _active;
    private readonly string _activeId;
    private readonly Dictionary<string, X509Certificate2> _byId = new(StringComparer.OrdinalIgnoreCase);

    public EnvelopeEncryption(IOptions<KeyProtectionOptions> options)
        : this(KeyStore.Load(options.Value.StoreLocation, options.Value.ActiveThumbprint),
            options.Value.PreviousThumbprints.Select(t => KeyStore.Load(options.Value.StoreLocation, t)))
    {
    }

    /// <summary>Certificados ya cargados (pruebas y rotación).</summary>
    internal EnvelopeEncryption(X509Certificate2 active, IEnumerable<X509Certificate2> previous)
    {
        _active = active;
        _activeId = KeyStore.Identifier(_active);
        _byId[_activeId] = _active;
        foreach (var cert in previous)
            _byId[KeyStore.Identifier(cert)] = cert;
    }

    public string ActiveKekId => _activeId;

    public EncryptedPayload Encrypt(ReadOnlySpan<byte> plaintext, Guid objectId, int versionNumber, byte component, PayloadKind kind)
    {
        var dek = RandomNumberGenerator.GetBytes(KeySize);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var ciphertext = new byte[plaintext.Length];
            using (var aes = new AesGcm(dek, TagSize))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(objectId, versionNumber, component, kind));
            }
            using var rsa = _active.GetRSAPublicKey() ?? throw new CryptographicException("El certificado KEK no tiene llave RSA.");
            var wrapped = rsa.Encrypt(dek, RSAEncryptionPadding.OaepSHA256);
            return new EncryptedPayload(objectId, versionNumber, component, kind, ciphertext, nonce, tag, wrapped, _activeId, AlgorithmName);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public byte[] Decrypt(EncryptedPayload payload)
    {
        if (!string.Equals(payload.Algorithm, AlgorithmName, StringComparison.Ordinal))
            throw new CryptographicException("Algoritmo de cifrado no admitido.");
        if (!_byId.TryGetValue(payload.KekThumbprint, out var kek))
            throw new CryptographicException("La KEK del valor no está disponible en este servidor.");

        using var rsa = kek.GetRSAPrivateKey() ?? throw new CryptographicException("El certificado KEK no tiene llave privada.");
        var dek = rsa.Decrypt(payload.WrappedDek, RSAEncryptionPadding.OaepSHA256);
        try
        {
            var plaintext = new byte[payload.Ciphertext.Length];
            using var aes = new AesGcm(dek, TagSize);
            aes.Decrypt(payload.Nonce, payload.Ciphertext, payload.Tag, plaintext,
                AssociatedData(payload.ObjectId, payload.VersionNumber, payload.Component, payload.PayloadKind));
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public void Dispose()
    {
        foreach (var cert in _byId.Values.Distinct())
            cert.Dispose();
    }

    /// <summary>Liga el texto cifrado a su objeto, versión, componente y forma: no puede trasladarse a otro registro.</summary>
    private static byte[] AssociatedData(Guid objectId, int versionNumber, byte component, PayloadKind kind)
    {
        var aad = new byte[4 + 16 + 4 + 1 + 1];
        "PV01"u8.CopyTo(aad);
        objectId.TryWriteBytes(aad.AsSpan(4, 16));
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(20, 4), versionNumber);
        aad[24] = component;
        aad[25] = (byte)kind;
        return aad;
    }
}

internal static class KeyStore
{
    public static X509Certificate2 Load(StoreLocation location, string thumbprint)
    {
        var clean = new string(thumbprint.Where(char.IsAsciiHexDigit).ToArray());
        if (clean.Length != 40)
            throw new InvalidOperationException("KeyProtection: la huella del certificado KEK no es válida (SHA-1 hexadecimal de 40 caracteres).");
        using var store = new X509Store(StoreName.My, location);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var found = store.Certificates.Find(X509FindType.FindByThumbprint, clean, validOnly: false);
        var cert = found.Count == 1 ? found[0] : throw new InvalidOperationException($"KeyProtection: no se encontró el certificado KEK {clean} en {location}\\My.");
        foreach (var other in found.Where(c => !ReferenceEquals(c, cert))) other.Dispose();

        if (!cert.HasPrivateKey)
            throw new InvalidOperationException("KeyProtection: el certificado KEK no tiene llave privada.");
        using var rsa = cert.GetRSAPublicKey() ?? throw new InvalidOperationException("KeyProtection: el certificado KEK debe ser RSA.");
        if (rsa.KeySize < KeyProtectionOptions.MinimumRsaKeySize)
            throw new InvalidOperationException($"KeyProtection: la llave RSA del KEK debe ser de al menos {KeyProtectionOptions.MinimumRsaKeySize} bits (IMP-19).");
        return cert;
    }

    /// <summary>Identificador estable del KEK guardado con cada valor: huella SHA-256 del certificado.</summary>
    public static string Identifier(X509Certificate2 cert) => cert.GetCertHashString(HashAlgorithmName.SHA256);
}
