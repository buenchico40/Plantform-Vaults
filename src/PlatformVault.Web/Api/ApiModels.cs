namespace PlatformVault.Web.Api;

// Modelos de lectura de la API. Los enumerados llegan con los valores del contrato (en español) y se muestran tal cual.
// Nunca contienen valores sensibles: el valor revelado solo existe en RevealedValue, que no se guarda ni se registra.

public sealed class Paged<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class LoginResponse
{
    public string SessionToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool MustChangePassword { get; set; }
}

public sealed class MeView
{
    public UserView User { get; set; } = new();
    public List<string> Permissions { get; set; } = [];
}

public sealed class UserView
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? ManagerUserId { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Roles { get; set; } = [];
}

public sealed class UserLookup
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public sealed class TemporaryCredential
{
    public Guid UserId { get; set; }
    public string TemporaryPassword { get; set; } = "";
}

public sealed class AreaView
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class SubtypeView
{
    public string Type { get; set; } = "";
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool HoldsValue { get; set; }
    public List<string> RequiredAttributes { get; set; } = [];
}

public class ObjectSummary
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Type { get; set; } = "";
    public string Subtype { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Criticality { get; set; } = "";
    public string Sensitivity { get; set; } = "";
    public string Environment { get; set; } = "";
    public Guid AreaId { get; set; }
    public string AreaName { get; set; } = "";
    public Guid? FunctionalOwnerId { get; set; }
    public string? FunctionalOwnerName { get; set; }
    public Guid? TechnicalOwnerId { get; set; }
    public string? TechnicalOwnerName { get; set; }
    public string LifecycleState { get; set; } = "";
    public string ExpirationStatus { get; set; } = "";
    public DateTime? ExpirationDate { get; set; }
    public bool NoExpirationJustified { get; set; }
    public string CustodyMode { get; set; } = "";
    public bool HasValue { get; set; }
    public int CurrentVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string ETag { get; set; } = "";

    public bool IsCritical => Criticality == "Crítico";
}

public sealed class ObjectDetail : ObjectSummary
{
    public string? ThumbprintSha256 { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = [];
    public List<GroupRef> Groups { get; set; } = [];
}

public sealed class GroupRef
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class ObjectCreated
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string ETag { get; set; } = "";
}

public sealed class WriteResult
{
    public string ETag { get; set; } = "";
    public int CurrentVersion { get; set; }
    public int RevokedAccesses { get; set; }
    public int CancelledRequests { get; set; }
}

public sealed class VersionEntry
{
    public int Version { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
    public string Reason { get; set; } = "";
    public List<string> ChangedFields { get; set; } = [];
}

public sealed class AuditEventView
{
    public long Sequence { get; set; }
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ActorType { get; set; } = "";
    public string? ActorName { get; set; }
    public string Action { get; set; } = "";
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string Result { get; set; } = "";
    public string? SourceIp { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? Details { get; set; }
}

public sealed class AuditIntegrityStatus
{
    public long EventsVerified { get; set; }
    public long? FirstBrokenSequence { get; set; }
    public bool IsIntact { get; set; }
    public DateTime CheckedAt { get; set; }
}

public sealed class GroupSummary
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int MemberCount { get; set; }
    public int ObjectCount { get; set; }
}

public sealed class GroupDetail
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ObjectCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GroupMember> Members { get; set; } = [];
}

public sealed class GroupMember
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsResponsible { get; set; }
    public DateTime AddedAt { get; set; }
}

public sealed class AccessRequestView
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; } = "";
    public Guid ObjectId { get; set; }
    public string ObjectCode { get; set; } = "";
    public string ObjectName { get; set; } = "";
    public string Criticality { get; set; } = "";
    public string Sensitivity { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Justification { get; set; }
    public DateTime RequestedStartAt { get; set; }
    public int RequestedDurationMinutes { get; set; }
    public string ApproverKind { get; set; } = "";
    public string State { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime PendingExpiresAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public List<DecisionView> Decisions { get; set; } = [];
}

public sealed class DecisionView
{
    public string ApproverName { get; set; } = "";
    public string Decision { get; set; } = "";
    public string? Comment { get; set; }
    public DateTime DecidedAt { get; set; }
}

public sealed class TemporaryAccessView
{
    public Guid Id { get; set; }
    public Guid ObjectId { get; set; }
    public string ObjectCode { get; set; } = "";
    public string ObjectName { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string State { get; set; } = "";
    public string? RevokeReason { get; set; }
}

public sealed class AlertView
{
    public Guid Id { get; set; }
    public Guid ObjectId { get; set; }
    public string ObjectCode { get; set; } = "";
    public string ObjectName { get; set; } = "";
    public string ObjectType { get; set; } = "";
    public DateTime? ExpirationDate { get; set; }
    public int? ThresholdDays { get; set; }
    public string Severity { get; set; } = "";
    public string State { get; set; } = "";
    public int EscalationLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgeComment { get; set; }
}

public sealed class ExpirationPolicyView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? AppliesToType { get; set; }
    public string? AppliesToCriticality { get; set; }
    public List<int> ThresholdDays { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public sealed class DashboardSummary
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Expired { get; set; }
    public int ExpiringSoon { get; set; }
    public int WithoutOwner { get; set; }
    public int OpenAlerts { get; set; }
    public int PendingRequests { get; set; }
    public List<CountByKey> ByType { get; set; } = [];
    public List<CountByKey> ByCriticality { get; set; } = [];
    public List<CountByKey> BySensitivity { get; set; } = [];
    public List<CountByKey> ByState { get; set; } = [];
    public List<CountByKey> ByExpirationStatus { get; set; } = [];
}

public sealed class CountByKey
{
    public string Key { get; set; } = "";
    public int Count { get; set; }
}

public sealed class RevealedValue
{
    public string Value { get; set; } = "";
    public int ExpiresInSeconds { get; set; }
}
