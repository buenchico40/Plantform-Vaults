using PlatformVault.Domain.Expiration;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Expiration;

public sealed record AlertView(
    Guid Id,
    Guid ObjectId,
    string ObjectCode,
    string ObjectName,
    ObjectType ObjectType,
    DateTime? ExpirationDate,
    int? ThresholdDays,
    AlertKind Kind,
    AlertSeverity Severity,
    AlertState State,
    int EscalationLevel,
    DateTime CreatedAt,
    DateTime? LastEscalatedAt,
    Guid? AcknowledgedBy,
    DateTime? AcknowledgedAt,
    string? AcknowledgeComment,
    DateTime? ResolvedAt,
    string? ResolutionReason);

public sealed record AlertRecord(Guid AlertId, Guid ObjectId, string AlertKey, AlertSeverity Severity, AlertState State, byte EscalationLevel);

public sealed record AlertSearchCriteria(AlertState? State, AlertSeverity? Severity, bool OnlyOpen, Guid? ObjectId = null);

public sealed record MonitoringCandidate(Guid ObjectId, string Code, string Name, ObjectType ObjectType, Criticality Criticality,
    DateTime ExpirationDate, Guid? FunctionalOwnerId, Guid? TechnicalOwnerId);

public sealed record ExpirationPolicyView(Guid Id, string Name, ObjectType? AppliesToType, Criticality? AppliesToCriticality,
    IReadOnlyList<int> ThresholdDays, bool IsActive, DateTime ModifiedAt);
