using PlatformVault.Domain.Access;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Access;

public sealed record AccessRequestView(
    Guid Id,
    string Code,
    Guid RequesterId,
    string RequesterName,
    Guid ObjectId,
    string ObjectCode,
    string ObjectName,
    Criticality Criticality,
    Sensitivity Sensitivity,
    AccessAction Action,
    string? Justification,
    DateTime RequestedStartAt,
    int RequestedDurationMinutes,
    ApproverKind ApproverKind,
    RequestState State,
    DateTime CreatedAt,
    DateTime PendingExpiresAt,
    DateTime? DecidedAt)
{
    public IReadOnlyList<ApprovalDecisionView> Decisions { get; init; } = [];
}

public sealed record ApprovalDecisionView(Guid ApproverId, string ApproverName, Decision Decision, string? Comment, DateTime DecidedAt);

public enum AccessRequestScope
{
    Mine,
    PendingForMe,
    All,
}

public sealed record UserContact(Guid UserId, string DisplayName, string? Email);

public sealed record NewAccessRequest(Guid RequestId, Guid RequesterId, Guid ObjectId, ValidatedAccessRequest Request, DateTime NowUtc);

public sealed record TemporaryAccessView(
    Guid Id,
    Guid OriginRequestId,
    Guid ObjectId,
    string ObjectCode,
    string ObjectName,
    AccessAction Action,
    DateTime StartAt,
    DateTime EndAt,
    TemporaryAccessState State,
    DateTime? RevokedAt,
    string? RevokeReason);

public sealed record TemporaryAccessRecord(Guid AccessId, Guid RequestId, Guid ObjectId, Guid BeneficiaryId, AccessAction Action,
    DateTime StartUtc, DateTime EndUtc, TemporaryAccessState State);

public sealed record ExpiredAccess(Guid AccessId, Guid ObjectId, Guid BeneficiaryId);

public sealed record ExpiredRequest(Guid RequestId, string Code, Guid RequesterId, Guid ObjectId);

/// <summary>Valor revelado (contrato: RevealedValue). Efímero: el cliente lo descarta tras mostrarlo (RN-084).</summary>
public sealed record RevealedValue(string Value, int ExpiresInSeconds, Guid AuditEventId);

/// <summary>Archivo descargado (llave privada o material de clave).</summary>
public sealed record DownloadedFile(string FileName, string ContentType, byte[] Content, Guid AuditEventId);
