namespace PlatformVault.Application.Auditing;

/// <summary>Evento de auditoría (RN-072). <see cref="Details"/> nunca contiene valores sensibles (RN-077).</summary>
public sealed record AuditEntry(
    Guid EventId,
    DateTime TimestampUtc,
    string ActorType,
    string? ActorId,
    string? ActorName,
    string Action,
    string? ResourceType,
    string? ResourceId,
    string Result,
    string? SourceIp,
    Guid? CorrelationId,
    string? Details);

public sealed record UsageEntry(Guid ObjectId, string ActorType, string ActorId, string Action, DateTime TimestampUtc, string Channel,
    string Result, string? SourceIp, Guid? CorrelationId);

public sealed record AuditEventView(long Sequence, Guid Id, DateTime Timestamp, string ActorType, string? ActorId, string? ActorName,
    string Action, string? ResourceType, string? ResourceId, string Result, string? SourceIp, Guid? CorrelationId, string? Details);

public sealed record UsageEventView(DateTime Timestamp, string ActorType, string ActorId, string Action, string Channel, string Result, string? SourceIp);

public sealed record AuditSearchCriteria(DateTime? FromUtc, DateTime? ToUtc, string? Action, string? ActorId, string? ResourceType,
    string? ResourceId, string? Result, Guid? CorrelationId = null);

public sealed record AuditIntegrityStatus(long EventsVerified, long? FirstBrokenSequence, long? FromSequence, long? ToSequence, DateTime CheckedAt)
{
    public bool IsIntact => FirstBrokenSequence is null;
}

public static class AuditResults
{
    public const string Success = "Success";
    public const string Denied = "Denied";
    public const string Failed = "Failed";
}

public static class ActorTypes
{
    public const string User = "User";
    public const string System = "System";
}

/// <summary>Acciones auditadas. Nombres estables para búsquedas y reportes.</summary>
public static class AuditActions
{
    public const string Login = "Auth.Login";
    public const string LoginFailed = "Auth.LoginFailed";
    public const string Logout = "Auth.Logout";
    public const string Reauthenticate = "Auth.Reauthenticate";
    public const string PasswordChanged = "Auth.PasswordChanged";
    public const string UserCreated = "User.Created";
    public const string UserUpdated = "User.Updated";
    public const string UserRolesChanged = "User.RolesChanged";
    public const string UserPasswordReset = "User.PasswordReset";
    public const string UserUnlocked = "User.Unlocked";
    public const string ObjectCreated = "Object.Created";
    public const string ObjectUpdated = "Object.Updated";
    public const string ObjectReclassified = "Object.Reclassified";
    public const string ObjectStateChanged = "Object.StateChanged";
    public const string ObjectOwnersChanged = "Object.OwnersChanged";
    public const string ObjectGroupsChanged = "Object.GroupsChanged";
    public const string ObjectValueChanged = "Object.ValueChanged";
    public const string ObjectValueRevealed = "Object.ValueRevealed";
    public const string ObjectFileDownloaded = "Object.FileDownloaded";
    public const string ObjectAccessDenied = "Object.AccessDenied";
    public const string GroupCreated = "Group.Created";
    public const string GroupUpdated = "Group.Updated";
    public const string GroupMemberAdded = "Group.MemberAdded";
    public const string GroupMemberRemoved = "Group.MemberRemoved";
    public const string AccessRequested = "Access.Requested";
    public const string AccessDecided = "Access.Decided";
    public const string AccessRequestCancelled = "Access.RequestCancelled";
    public const string AccessRequestExpired = "Access.RequestExpired";
    public const string TemporaryAccessRevoked = "Access.TemporaryRevoked";
    public const string TemporaryAccessExpired = "Access.TemporaryExpired";
    public const string PolicyChanged = "Policy.ExpirationChanged";
    public const string AlertRaised = "Alert.Raised";
    public const string AlertAcknowledged = "Alert.Acknowledged";
    public const string AlertEscalated = "Alert.Escalated";
    public const string AuditChainVerified = "Audit.ChainVerified";
    public const string AuditChainBroken = "Audit.ChainBroken";
    public const string MassAssignmentRejected = "Api.MassAssignmentRejected";
    public const string ApiClientRejected = "Api.ClientRejected";
}
