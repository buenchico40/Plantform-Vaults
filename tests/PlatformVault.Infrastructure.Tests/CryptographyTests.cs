using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Objects;
using PlatformVault.Infrastructure.Cryptography;

namespace PlatformVault.Infrastructure.Tests;

internal static class TestCertificates
{
    public static X509Certificate2 Rsa(int bits = 3072, string subject = "CN=PlatformVault KEK Test")
    {
        using var rsa = RSA.Create(bits);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("pagos.banco.local");
        request.CertificateExtensions.Add(san.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
    }
}

public sealed class EnvelopeEncryptionTests : IDisposable
{
    private readonly X509Certificate2 _kek = TestCertificates.Rsa();
    private readonly EnvelopeEncryption _crypto;

    public EnvelopeEncryptionTests() => _crypto = new EnvelopeEncryption(_kek, []);

    [Fact]
    public void Round_trip_with_fresh_key_and_nonce_each_time_IMP30()
    {
        var id = Guid.NewGuid();
        var plain = Encoding.UTF8.GetBytes("valor-super-secreto");
        var a = _crypto.Encrypt(plain, id, 1, 0, PayloadKind.Text);
        var b = _crypto.Encrypt(plain, id, 1, 0, PayloadKind.Text);
        Assert.NotEqual(a.Ciphertext, b.Ciphertext);
        Assert.NotEqual(a.WrappedDek, b.WrappedDek);
        Assert.NotEqual(a.Nonce, b.Nonce);
        Assert.Equal(12, a.Nonce.Length);
        Assert.Equal(16, a.Tag.Length);
        Assert.Equal(64, a.KekThumbprint.Length);
        Assert.DoesNotContain("valor", Encoding.UTF8.GetString(a.Ciphertext), StringComparison.Ordinal);
        Assert.Equal(plain, _crypto.Decrypt(a));
    }

    [Fact]
    public void Ciphertext_cannot_be_moved_to_another_object_or_version()
    {
        var payload = _crypto.Encrypt("x"u8, Guid.NewGuid(), 1, 0, PayloadKind.Text);
        Assert.ThrowsAny<CryptographicException>(() => _crypto.Decrypt(payload with { ObjectId = Guid.NewGuid() }));
        Assert.ThrowsAny<CryptographicException>(() => _crypto.Decrypt(payload with { VersionNumber = 2 }));
        Assert.ThrowsAny<CryptographicException>(() => _crypto.Decrypt(payload with { Component = 1 }));
    }

    [Fact]
    public void Tampering_is_detected_by_gcm_tag()
    {
        var payload = _crypto.Encrypt("secreto"u8, Guid.NewGuid(), 1, 0, PayloadKind.Text);
        var tampered = payload.Ciphertext.ToArray();
        tampered[0] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => _crypto.Decrypt(payload with { Ciphertext = tampered }));
    }

    [Fact]
    public void Unknown_kek_is_rejected()
    {
        var payload = _crypto.Encrypt("secreto"u8, Guid.NewGuid(), 1, 0, PayloadKind.Text);
        Assert.Throws<CryptographicException>(() => _crypto.Decrypt(payload with { KekThumbprint = new string('0', 64) }));
    }

    [Fact]
    public void Previous_kek_still_decrypts_after_rotation_IMP31()
    {
        var payload = _crypto.Encrypt("antes de rotar"u8, Guid.NewGuid(), 3, 0, PayloadKind.Text);
        using var newKek = TestCertificates.Rsa();
        using var rotated = new EnvelopeEncryption(newKek, [_kek]);
        Assert.Equal("antes de rotar"u8.ToArray(), rotated.Decrypt(payload));
        Assert.NotEqual(payload.KekThumbprint, rotated.ActiveKekId);
    }

    public void Dispose()
    {
        _crypto.Dispose();
        _kek.Dispose();
    }
}

public sealed class CertificateInspectorTests
{
    private readonly CertificateInspector _inspector = new();

    [Fact]
    public void Extracts_metadata_from_public_certificate_RN016()
    {
        using var cert = TestCertificates.Rsa(2048, "CN=pagos.banco.local");
        var (info, pkcs12) = _inspector.Inspect(cert.Export(X509ContentType.Cert), null);
        Assert.Null(pkcs12);
        Assert.Equal("CN=pagos.banco.local", info.Subject);
        Assert.Contains("pagos.banco.local", info.SubjectAlternativeNames);
        Assert.Equal(64, info.ThumbprintSha256.Length);
        Assert.Equal(2048, info.KeySize);
        Assert.False(info.HasPrivateKey);
        Assert.Equal(DateTimeKind.Utc, info.NotAfterUtc.Kind);
        Assert.Equal(cert.RawData, info.CertificateDer);
    }

    [Fact]
    public void Pkcs12_is_reexported_and_download_is_protected_with_new_password_RN015()
    {
        using var cert = TestCertificates.Rsa(2048);
        var pfx = cert.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, "contenedor-original");
        var (info, stored) = _inspector.Inspect(pfx, "contenedor-original");
        Assert.True(info.HasPrivateKey);
        Assert.NotNull(stored);

        var download = _inspector.ExportPkcs12(stored!, "clave-de-descarga-123");
        using var reloaded = X509CertificateLoader.LoadPkcs12(download, "clave-de-descarga-123", X509KeyStorageFlags.EphemeralKeySet);
        Assert.True(reloaded.HasPrivateKey);
        Assert.Equal(cert.Thumbprint, reloaded.Thumbprint);
        Assert.ThrowsAny<CryptographicException>(() => X509CertificateLoader.LoadPkcs12(download, "contenedor-original", X509KeyStorageFlags.EphemeralKeySet));
    }

    [Fact]
    public void Wrong_container_password_is_a_business_error()
    {
        using var cert = TestCertificates.Rsa(2048);
        var pfx = cert.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, "correcta");
        Assert.Throws<DomainException>(() => _inspector.Inspect(pfx, "incorrecta"));
    }

    [Fact]
    public void Garbage_is_rejected() => Assert.Throws<DomainException>(() => _inspector.Inspect("no es un certificado"u8.ToArray(), null));
}
