using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;
using PlatformVault.Application.Common;
using PlatformVault.Application.Notifications;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Identity;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Objects;

public sealed record CreateObjectCommand(
    ObjectType Type,
    string Subtype,
    string Name,
    string? Description,
    Criticality Criticality,
    Sensitivity Sensitivity,
    DeploymentEnvironment Environment,
    Guid AreaId,
    Guid? OwnerId,
    CustodyMode CustodyMode,
    DateTime? ExpirationDate,
    bool NoExpirationJustified,
    IReadOnlyDictionary<string, string>? Attributes,
    SecretInput? Value);

public sealed record ObjectCreated(Guid Id, string Code, string ETag);

/// <summary>US-001 a US-005: registro de objeto en estado Borrador, con valor cifrado opcional.</summary>
public sealed class CreateObjectHandler(
    ICurrentUser user,
    IClock clock,
    IObjectRepository objects,
    ISecretPayloadStore payloads,
    IEnvelopeEncryption encryption,
    ICertificateInspector certificates,
    OwnerValidator owners,
    IUnitOfWork unitOfWork,
    AuditLogger audit,
    Notifier notifier) : ICommandHandler<CreateObjectCommand, ObjectCreated>
{
    public async Task<ObjectCreated> HandleAsync(CreateObjectCommand command, CancellationToken ct)
    {
        user.Require(Permission.CreateObject);
        // Nota ² de la matriz §4.2: el Custodio registra objetos solo en su área.
        // IMP-61: quien registra el objeto lo sigue viendo (app.ufn_VisibleObjects), sin necesidad de ser propietario.
        if (user.AreaId != command.AreaId)
            throw new ForbiddenFailure("Solo puede registrar objetos en su área.");

        await owners.ValidateAsync(command.OwnerId, ct);

        var definition = ObjectCatalog.Get(command.Type, command.Subtype);
        var prepared = PayloadPreparation.Prepare(command.Value, command.Type, definition.PayloadKind, certificates);
        try
        {
            var attributes = command.Attributes is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(command.Attributes, StringComparer.Ordinal);
            var expiration = command.ExpirationDate;
            string? thumbprint = null;
            if (prepared.Certificate is { } cert)
            {
                foreach (var (k, v) in PayloadPreparation.CertificateAttributes(cert)) attributes[k] = v;
                expiration = cert.NotAfterUtc; // RN-016: la expiración es NotAfter y no se edita.
                thumbprint = cert.ThumbprintSha256;
            }

            var obj = ManagedObject.Register(new ObjectRegistration(command.Type, command.Subtype, command.Name, command.Description,
                command.Criticality, command.Sensitivity, command.Environment, command.AreaId, command.OwnerId, command.CustodyMode,
                expiration, command.NoExpirationJustified, attributes, thumbprint), Guid.NewGuid());

            if (prepared.Bytes is not null)
            {
                obj.EnsureCanStorePayload();
                obj.HasPayload = true;
            }

            var (nameExists, thumbprintExists) = await objects.ExistsDuplicateAsync(obj.ObjectType, obj.Environment, obj.AreaId, obj.Name,
                obj.Thumbprint, null, ct);
            if (nameExists)
                throw new ConflictFailure("DUPLICATE_NAME", "Ya existe un objeto con ese nombre para el mismo tipo, ambiente y área (RN-005).");
            if (thumbprintExists)
                throw new ConflictFailure("DUPLICATE_THUMBPRINT", "Ya existe un certificado activo con la misma huella SHA-256 (RN-018).");

            var now = clock.UtcNow;
            await using var tx = await unitOfWork.BeginAsync(ct);
            var result = await objects.InsertAsync(obj, user.UserId, now, "Alta del objeto", "[\"created\"]", ct);
            if (prepared.Bytes is not null)
            {
                var encrypted = encryption.Encrypt(prepared.Bytes, obj.ObjectId, result.CurrentVersion, 0, obj.EnsureCanStorePayload());
                await payloads.InsertAsync(encrypted, user.UserId, ct);
            }
            if (prepared.Certificate is { } publicCertificate)
                await objects.InsertPublicCertificateAsync(obj.ObjectId, result.CurrentVersion, publicCertificate.CertificateDer, ct);
            await audit.SuccessAsync(AuditActions.ObjectCreated, "ManagedObject", obj.ObjectId.ToString(), new
            {
                code = result.Code,
                type = obj.ObjectType.ToString(),
                subtype = obj.Subtype,
                criticality = obj.Criticality.ToString(),
                sensitivity = obj.Sensitivity.ToString(),
                hasValue = obj.HasPayload,
            }, ct);
            await notifier.NotifySecurityAsync(obj.IsCritical, result.Code ?? obj.ObjectId.ToString(), "alta", ct);
            await tx.CommitAsync(ct);

            return new ObjectCreated(obj.ObjectId, result.Code ?? string.Empty, result.ETag);
        }
        finally
        {
            prepared.Clear();
            command.Value?.Clear();
        }
    }
}

/// <summary>Valida que los propietarios existan, estén activos y no tengan roles exclusivos (RN-087, §4.3).</summary>
public sealed class OwnerValidator(IUserDirectory users)
{
    public async Task ValidateAsync(Guid? ownerId, CancellationToken ct)
    {
        if (ownerId is not { } id)
            return;
        var owner = await users.GetAsync(id, ct)
            ?? throw new DomainException(DomainErrors.OwnersRequired, "El propietario indicado no existe.");
        SegregationOfDuties.EnsureCanOwn(owner.Roles, owner.IsActive);
    }
}
