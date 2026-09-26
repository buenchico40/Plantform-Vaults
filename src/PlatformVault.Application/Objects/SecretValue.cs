using System.Security.Cryptography;
using System.Text;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Access;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;
using PlatformVault.Application.Notifications;
using PlatformVault.Domain.Access;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Objects;

public sealed record UpdateObjectValueCommand(Guid ObjectId, string ETag, SecretInput Value, DateTime? NewExpirationDate, string Reason);

public sealed record RevealValueCommand(Guid ObjectId, Guid? TemporaryAccessId);

/// <summary>Parte a descargar (contrato: downloadObjectFile?part=...).</summary>
public enum DownloadPart
{
    PublicCertificate,
    PrivateKeyPfx,
    KeyMaterial,
}

public sealed record DownloadObjectFileCommand(Guid ObjectId, DownloadPart Part, Guid? TemporaryAccessId, string? DownloadPassword);

/// <summary>US-014/US-015: actualización o renovación del valor custodiado (nueva versión, RN-093).</summary>
public sealed class UpdateObjectValueHandler(ICurrentUser user, IClock clock, ObjectCommandContext loader, IObjectRepository objects,
    ISecretPayloadStore payloads, IEnvelopeEncryption encryption, ICertificateInspector certificates, IUnitOfWork unitOfWork,
    AuditLogger audit, Notifier notifier) : ICommandHandler<UpdateObjectValueCommand, ObjectWriteResult>
{
    public async Task<ObjectWriteResult> HandleAsync(UpdateObjectValueCommand command, CancellationToken ct)
    {
        PreparedPayload? prepared = null;
        try
        {
            var reason = UpdateObjectHandler.RequireReason(command.Reason);
            var (_, obj, rowVer) = await loader.LoadAsync(command.ObjectId, command.ETag, ObjectOperation.EditValue, ct);
            var kind = obj.EnsureCanStorePayload();
            prepared = PayloadPreparation.Prepare(command.Value, obj.ObjectType, kind, certificates);
            if (prepared.Bytes is null)
                throw new DomainException(DomainErrors.PayloadNotAllowed,
                    obj.ObjectType == ObjectType.Certificate
                        ? "El archivo debe ser un PKCS#12 con llave privada."
                        : "Debe indicar el nuevo valor.");

            var updateCertificate = prepared.Certificate is not null;
            var updateExpiration = updateCertificate || command.NewExpirationDate is not null;
            if (prepared.Certificate is { } cert)
            {
                var (_, thumbprintExists) = await objects.ExistsDuplicateAsync(obj.ObjectType, obj.Environment, obj.AreaId, obj.Name,
                    cert.ThumbprintSha256, obj.ObjectId, ct);
                if (thumbprintExists)
                    throw new ConflictFailure("DUPLICATE_THUMBPRINT", "Ya existe un certificado activo con la misma huella SHA-256 (RN-018).");
                var attributes = new Dictionary<string, string>(obj.Details, StringComparer.Ordinal);
                foreach (var (k, v) in PayloadPreparation.CertificateAttributes(cert)) attributes[k] = v;
                obj.Details = attributes;
                obj.Thumbprint = cert.ThumbprintSha256;
                obj.ExpirationDate = cert.NotAfterUtc;
                obj.NoExpirationJustified = false;
            }
            else if (command.NewExpirationDate is { } newExpiration)
            {
                if (newExpiration <= clock.UtcNow)
                    throw new DomainException(DomainErrors.InvalidValue, "La nueva fecha de expiración debe ser futura.");
                obj.ExpirationDate = newExpiration;
                obj.NoExpirationJustified = false;
            }

            await using var tx = await unitOfWork.BeginAsync(ct);
            var result = await objects.BumpVersionForPayloadAsync(obj, rowVer, user.UserId, clock.UtcNow, reason, updateExpiration,
                updateCertificate, ct);
            var encrypted = encryption.Encrypt(prepared.Bytes, obj.ObjectId, result.CurrentVersion, 0, kind);
            await payloads.InsertAsync(encrypted, user.UserId, ct);
            if (prepared.Certificate is { } publicCertificate)
                await objects.InsertPublicCertificateAsync(obj.ObjectId, result.CurrentVersion, publicCertificate.CertificateDer, ct);
            await audit.SuccessAsync(AuditActions.ObjectValueChanged, "ManagedObject", obj.ObjectId.ToString(), new
            {
                code = obj.Code,
                version = result.CurrentVersion,
                value = "[PROTEGIDO]", // RN-077
                renewed = updateExpiration,
                reason,
            }, ct);
            await notifier.NotifySecurityAsync(obj.IsCritical, obj.Code, "cambio de valor", ct);
            await notifier.NotifyGroupActivityAsync(obj.ObjectId, obj.Code, "modificar valor", ct);
            await tx.CommitAsync(ct);
            return result;
        }
        finally
        {
            prepared?.Clear();
            command.Value.Clear();
        }
    }
}

/// <summary>
/// Acceso al valor custodiado: exige ámbito, acceso temporal vigente para la acción (RN-042), objeto activo,
/// grupo con dos miembros activos para Críticos y Restringidos (RN-104) y re-autenticación reciente (IMP-29).
/// La auditoría y el evento de uso se confirman antes de entregar el valor (fail-closed, RN-079).
/// </summary>
public sealed class PayloadAccessGuard(ICurrentUser user, IClock clock, ObjectAuthorizer authorizer, AuditLogger audit)
{
    public async Task<(ObjectAuthorizationContext Context, ActiveAccessInfo Access)> AuthorizeAsync(Guid objectId, Guid? temporaryAccessId,
        IReadOnlyCollection<AccessAction> acceptedActions, CancellationToken ct)
    {
        var context = await authorizer.AuthorizeAsync(objectId, ObjectOperation.RequestAccess, ct);
        user.EnsureRecentlyReauthenticated(clock.UtcNow);

        string? denial = null;
        if (context.LifecycleState != LifecycleState.Active)
            denial = "El objeto no está activo (RN-009).";
        else if (!context.HasPayload || context.CustodyMode != CustodyMode.Internal)
            denial = "El objeto no tiene un valor custodiado.";
        else if ((context.Criticality == Criticality.Critical || context.Sensitivity == Sensitivity.Restricted)
                 && context.MaxActiveMembersInAGroup < 2)
            denial = "El objeto está bloqueado: su grupo no tiene dos miembros activos (RN-104).";

        var access = context.ActiveAccesses.FirstOrDefault(a => acceptedActions.Contains(a.Action)
            && (temporaryAccessId is null || a.AccessId == temporaryAccessId));
        if (denial is null && access is null)
            denial = "No tiene un acceso temporal aprobado y vigente para esta acción (RN-042).";

        if (denial is not null)
        {
            await audit.DeniedAsync(AuditActions.ObjectAccessDenied, "ManagedObject", objectId.ToString(),
                new { operation = string.Join('/', acceptedActions), reason = denial }, ct);
            throw new ForbiddenFailure(denial);
        }
        return (context, access!);
    }
}

/// <summary>US-016: revelar el valor (texto) durante un máximo de 30 s en pantalla (RN-084).</summary>
public sealed class RevealValueHandler(PayloadAccessGuard guard, ISecretPayloadStore payloads, IEnvelopeEncryption encryption,
    IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier) : ICommandHandler<RevealValueCommand, RevealedValue>
{
    public const int DisplaySeconds = 30;

    public async Task<RevealedValue> HandleAsync(RevealValueCommand command, CancellationToken ct)
    {
        var (context, access) = await guard.AuthorizeAsync(command.ObjectId, command.TemporaryAccessId, [AccessAction.Reveal], ct);
        var payload = await payloads.GetLatestAsync(context.ObjectId, 0, ct) ?? throw new NotFoundFailure("valor");
        if (payload.PayloadKind != PayloadKind.Text)
            throw new ForbiddenFailure("El valor de este objeto se descarga como archivo.");

        var plaintext = encryption.Decrypt(payload);
        try
        {
            var value = Encoding.UTF8.GetString(plaintext);
            await using var tx = await unitOfWork.BeginAsync(ct);
            var eventId = await audit.SuccessAsync(AuditActions.ObjectValueRevealed, "ManagedObject", context.ObjectId.ToString(),
                new { code = context.Code, version = payload.VersionNumber, temporaryAccessId = access.AccessId }, ct);
            await audit.RecordUsageAsync(context.ObjectId, AccessAction.Reveal.ToString(), ct);
            await notifier.NotifyGroupActivityAsync(context.ObjectId, context.Code, "revelar", ct);
            await tx.CommitAsync(ct);
            return new RevealedValue(value, DisplaySeconds, eventId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}

/// <summary>
/// US-017: descarga del certificado público (permiso Consultar), de la llave privada (PKCS#12 protegido con la contraseña
/// que elige quien descarga) o del material de clave (acceso temporal aprobado).
/// </summary>
public sealed class DownloadObjectFileHandler(PayloadAccessGuard guard, ObjectAuthorizer authorizer, IObjectRepository objects,
    ISecretPayloadStore payloads, IEnvelopeEncryption encryption, ICertificateInspector certificates, IUnitOfWork unitOfWork,
    AuditLogger audit, Notifier notifier) : ICommandHandler<DownloadObjectFileCommand, DownloadedFile>
{
    public const int MinDownloadPasswordLength = 12;

    public async Task<DownloadedFile> HandleAsync(DownloadObjectFileCommand command, CancellationToken ct)
    {
        if (command.Part == DownloadPart.PublicCertificate)
            return await DownloadPublicCertificateAsync(command.ObjectId, ct);

        var requested = command.Part == DownloadPart.PrivateKeyPfx ? AccessAction.DownloadPrivateKey : AccessAction.DownloadKeyMaterial;
        var (context, access) = await guard.AuthorizeAsync(command.ObjectId, command.TemporaryAccessId, [requested], ct);
        var payload = await payloads.GetLatestAsync(context.ObjectId, 0, ct) ?? throw new NotFoundFailure("valor");
        if (AccessPolicy.ActionFor(payload.PayloadKind) != access.Action)
            throw new ForbiddenFailure("El acceso temporal no corresponde al tipo de valor del objeto.");
        if (payload.PayloadKind == PayloadKind.Pkcs12 && (command.DownloadPassword?.Length ?? 0) < MinDownloadPasswordLength)
            throw new ValidationFailure($"Indique una contraseña de al menos {MinDownloadPasswordLength} caracteres para proteger el archivo.");

        var plaintext = encryption.Decrypt(payload);
        try
        {
            var (content, fileName, contentType) = payload.PayloadKind switch
            {
                PayloadKind.Pkcs12 => (certificates.ExportPkcs12(plaintext, command.DownloadPassword!), context.Code + ".pfx", "application/x-pkcs12"),
                PayloadKind.KeyMaterial => (plaintext.ToArray(), context.Code + ".key", "application/octet-stream"),
                _ => throw new ForbiddenFailure("El valor de este objeto se revela, no se descarga."),
            };

            await using var tx = await unitOfWork.BeginAsync(ct);
            var eventId = await audit.SuccessAsync(AuditActions.ObjectFileDownloaded, "ManagedObject", context.ObjectId.ToString(),
                new { code = context.Code, version = payload.VersionNumber, action = access.Action.ToString(), temporaryAccessId = access.AccessId }, ct);
            await audit.RecordUsageAsync(context.ObjectId, access.Action.ToString(), ct);
            await notifier.NotifyGroupActivityAsync(context.ObjectId, context.Code, "descargar", ct);
            await tx.CommitAsync(ct);
            return new DownloadedFile(fileName, contentType, content, eventId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>El certificado público no es un valor sensible: basta con poder consultar el objeto. Se audita la descarga.</summary>
    private async Task<DownloadedFile> DownloadPublicCertificateAsync(Guid objectId, CancellationToken ct)
    {
        var context = await authorizer.AuthorizeAsync(objectId, ObjectOperation.View, ct);
        if (context.ObjectType != ObjectType.Certificate)
            throw new ValidationFailure("Solo los certificados tienen certificado público.");
        var der = await objects.GetPublicCertificateAsync(objectId, ct) ?? throw new NotFoundFailure("certificado público");
        var eventId = await audit.SuccessAsync(AuditActions.ObjectFileDownloaded, "ManagedObject", objectId.ToString(),
            new { code = context.Code, part = DownloadPart.PublicCertificate.ToString() }, ct);
        return new DownloadedFile(context.Code + ".cer", "application/pkix-cert", der, eventId);
    }
}
