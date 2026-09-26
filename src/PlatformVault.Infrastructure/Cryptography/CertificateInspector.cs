using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Objects;
using PlatformVault.Domain.Common;

namespace PlatformVault.Infrastructure.Cryptography;

/// <summary>
/// Lectura de certificados (RN-015, RN-016). Un PKCS#12 se abre con su contraseña solo en memoria y se re-exporta
/// sin contraseña para cifrarse con la KEK; la contraseña original nunca se persiste.
/// </summary>
public sealed class CertificateInspector : ICertificateInspector
{
    private const X509KeyStorageFlags Flags = X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable;

    public (CertificateInfo Info, byte[]? Pkcs12WithoutPassword) Inspect(byte[] file, string? containerPassword)
    {
        try
        {
            if (X509Certificate2.GetCertContentType(file) == X509ContentType.Pkcs12)
            {
                using var collection = new DisposableCollection(X509CertificateLoader.LoadPkcs12Collection(file, containerPassword, Flags));
                var leaf = collection.Items.FirstOrDefault(c => c.HasPrivateKey) ?? collection.Items.FirstOrDefault()
                    ?? throw new DomainException(DomainErrors.InvalidValue, "El archivo PKCS#12 no contiene certificados.");
                var export = leaf.HasPrivateKey ? collection.Items.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, string.Empty) : null;
                return (Describe(leaf), export);
            }

            using var certificate = X509CertificateLoader.LoadCertificate(file);
            return (Describe(certificate), null);
        }
        catch (CryptographicException)
        {
            throw new DomainException(DomainErrors.InvalidValue,
                "No se pudo leer el certificado. Verifique el formato (PEM, DER, CER/CRT, PFX/P12) y la contraseña del contenedor.");
        }
    }

    public byte[] ExportPkcs12(byte[] pkcs12WithoutPassword, string downloadPassword)
    {
        using var collection = new DisposableCollection(X509CertificateLoader.LoadPkcs12Collection(pkcs12WithoutPassword, string.Empty, Flags));
        return collection.Items.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, downloadPassword);
    }

    private static CertificateInfo Describe(X509Certificate2 cert)
    {
        var san = cert.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        var names = san is null
            ? []
            : san.EnumerateDnsNames().Concat(san.EnumerateIPAddresses().Select(ip => ip.ToString())).ToList();
        var keySize = 0;
        using (var rsa = cert.GetRSAPublicKey()) keySize = rsa?.KeySize ?? 0;
        if (keySize == 0)
        {
            using var ecdsa = cert.GetECDsaPublicKey();
            keySize = ecdsa?.KeySize ?? 0;
        }
        return new CertificateInfo(
            cert.Subject,
            cert.Issuer,
            cert.SerialNumber,
            names,
            cert.NotBefore.ToUniversalTime(),
            cert.NotAfter.ToUniversalTime(),
            cert.GetCertHashString(HashAlgorithmName.SHA256),
            cert.SignatureAlgorithm.FriendlyName ?? cert.SignatureAlgorithm.Value ?? string.Empty,
            cert.PublicKey.Oid.FriendlyName ?? cert.PublicKey.Oid.Value ?? string.Empty,
            keySize,
            cert.HasPrivateKey,
            cert.RawData);
    }

    private sealed class DisposableCollection(X509Certificate2Collection items) : IDisposable
    {
        public X509Certificate2Collection Items { get; } = items;

        public void Dispose()
        {
            foreach (var cert in Items) cert.Dispose();
        }
    }
}
