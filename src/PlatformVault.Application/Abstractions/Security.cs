using PlatformVault.Application.Objects;
using PlatformVault.Application.Users;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Abstractions;

/// <summary>Valor cifrado con cifrado de sobre (IMP-30). Solo contiene material cifrado.</summary>
public sealed record EncryptedPayload(
    Guid ObjectId,
    int VersionNumber,
    byte Component,
    PayloadKind PayloadKind,
    byte[] Ciphertext,
    byte[] Nonce,
    byte[] Tag,
    byte[] WrappedDek,
    string KekThumbprint,
    string Algorithm);

/// <summary>
/// Cifrado de sobre: DEK AES-256-GCM por objeto, versión y componente, envuelta con RSA-OAEP-SHA256
/// con el certificado KEK local (DEC-35). Los datos asociados ligan el texto cifrado a su objeto y versión.
/// </summary>
public interface IEnvelopeEncryption
{
    EncryptedPayload Encrypt(ReadOnlySpan<byte> plaintext, Guid objectId, int versionNumber, byte component, PayloadKind kind);

    /// <summary>Descifra en un búfer que el llamador debe limpiar con <c>CryptographicOperations.ZeroMemory</c>.</summary>
    byte[] Decrypt(EncryptedPayload payload);
}

/// <summary>Lectura de certificados con las primitivas de .NET (RN-015, RN-016).</summary>
public interface ICertificateInspector
{
    /// <summary>Lee el archivo; si es PKCS#12 lo abre con su contraseña y devuelve la llave privada re-exportada sin contraseña.</summary>
    (CertificateInfo Info, byte[]? Pkcs12WithoutPassword) Inspect(byte[] file, string? containerPassword);

    /// <summary>Exporta el PKCS#12 custodiado protegido con la contraseña que elige quien descarga.</summary>
    byte[] ExportPkcs12(byte[] pkcs12WithoutPassword, string downloadPassword);
}

/// <summary>Identidad local de la Fase 1 (IMP-04, IMP-22). Entra ID se añadirá detrás de esta interfaz en la Fase 2.</summary>
public interface IIdentityService
{
    Task<LoginResult> PasswordSignInAsync(string userName, string password, string? clientIp, string? userAgent, CancellationToken ct);
    Task<bool> ReauthenticateAsync(Guid sessionId, Guid userId, string password, CancellationToken ct);
    Task<IReadOnlyList<string>> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
    Task<(Guid UserId, IReadOnlyList<string> Errors)> CreateUserAsync(NewUser user, string temporaryPassword, Guid actorId, CancellationToken ct);
    Task<IReadOnlyList<string>> UpdateUserAsync(Guid userId, UserUpdate update, Guid actorId, CancellationToken ct);
    Task<IReadOnlyList<string>> SetRolesAsync(Guid userId, IReadOnlyCollection<string> roles, Guid actorId, CancellationToken ct);
    Task<IReadOnlyList<string>> ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken ct);
    Task UnlockAsync(Guid userId, CancellationToken ct);
}

public interface ISessionService
{
    Task<SessionPrincipal?> ValidateAsync(string token, CancellationToken ct);
    Task RevokeAsync(Guid sessionId, string reason, CancellationToken ct);
    Task RevokeAllForUserAsync(Guid userId, string reason, Guid? exceptSessionId, CancellationToken ct);
}
