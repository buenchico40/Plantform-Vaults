using PlatformVault.Application.Access;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Expiration;
using PlatformVault.Application.Groups;
using PlatformVault.Application.Objects;
using PlatformVault.Application.Users;
using PlatformVault.Domain.Access;
using PlatformVault.Domain.Expiration;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Abstractions;

// Todos los repositorios se implementan con Dapper y procedimientos almacenados (IMP-03).

/// <summary>Transacción de la petición. Las escrituras y su auditoría se confirman juntas (RN-079).</summary>
public interface IUnitOfWork
{
    Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken);
}

public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IObjectRepository
{
    Task<ObjectAuthorizationContext?> GetAuthorizationContextAsync(Guid objectId, Guid viewerId, DateTime nowUtc, CancellationToken ct);
    Task<ManagedObject?> LoadAsync(Guid objectId, CancellationToken ct);
    Task<ObjectDetail?> GetDetailAsync(Guid objectId, VisibilityScope scope, DateTime nowUtc, CancellationToken ct);
    Task<PagedResult<ObjectSummary>> SearchAsync(ObjectSearchCriteria criteria, PageRequest page, VisibilityScope scope, DateTime nowUtc, CancellationToken ct);
    Task<(bool NameExists, bool ThumbprintExists)> ExistsDuplicateAsync(ObjectType type, DeploymentEnvironment environment, Guid areaId,
        string name, string? thumbprint, Guid? excludeObjectId, CancellationToken ct);
    Task<ObjectWriteResult> InsertAsync(ManagedObject obj, Guid actorId, DateTime nowUtc, string reason, string changedFieldsJson, CancellationToken ct);
    Task<ObjectWriteResult> UpdateMetadataAsync(ManagedObject obj, byte[] expectedRowVer, Guid actorId, DateTime nowUtc, string reason,
        string changedFieldsJson, bool resolveOpenAlerts, CancellationToken ct);
    Task<StateChangeResult> ChangeStateAsync(Guid objectId, LifecycleState newState, byte[] expectedRowVer, Guid actorId, DateTime nowUtc,
        string reason, CancellationToken ct);
    Task<ObjectWriteResult> SetOwnersAsync(Guid objectId, Guid functionalOwnerId, Guid technicalOwnerId, byte[] expectedRowVer, Guid actorId,
        DateTime nowUtc, string reason, CancellationToken ct);
    Task<ObjectWriteResult> SetGroupsAsync(Guid objectId, IReadOnlyCollection<Guid> groupIds, byte[] expectedRowVer, Guid actorId, DateTime nowUtc,
        string reason, CancellationToken ct);
    /// <summary>Nueva versión por cambio de valor. Si <paramref name="updateCertificate"/>, guarda también huella, atributos y expiración del certificado.</summary>
    Task<ObjectWriteResult> BumpVersionForPayloadAsync(ManagedObject obj, byte[] expectedRowVer, Guid actorId, DateTime nowUtc, string reason,
        bool updateExpiration, bool updateCertificate, CancellationToken ct);
    /// <summary>Certificado público (DER) de una versión; no es un valor sensible (IMP-42).</summary>
    Task InsertPublicCertificateAsync(Guid objectId, int versionNumber, byte[] certificateDer, CancellationToken ct);
    Task<byte[]?> GetPublicCertificateAsync(Guid objectId, CancellationToken ct);
    Task<IReadOnlyList<ObjectVersionEntry>> ListVersionsAsync(Guid objectId, VisibilityScope scope, CancellationToken ct);
    Task<IReadOnlyList<OwnershipChange>> ListOwnershipHistoryAsync(Guid objectId, VisibilityScope scope, CancellationToken ct);
}

/// <summary>Contenido cifrado del esquema vault (IMP-18). Nunca recibe ni devuelve texto plano.</summary>
public interface ISecretPayloadStore
{
    Task InsertAsync(EncryptedPayload payload, Guid actorId, CancellationToken ct);
    Task<EncryptedPayload?> GetLatestAsync(Guid objectId, byte component, CancellationToken ct);
}

public interface IGroupRepository
{
    Task<GroupDetail?> GetAsync(Guid groupId, Guid viewerId, bool hasGlobalScope, CancellationToken ct);
    Task<PagedResult<GroupSummary>> SearchAsync(string? text, bool? isActive, PageRequest page, Guid viewerId, bool hasGlobalScope, CancellationToken ct);
    Task<IReadOnlyList<GroupCapacity>> GetCapacitiesAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct);
    Task<string> InsertAsync(Guid groupId, string name, string? description, Guid? areaId, Guid responsibleUserId, Guid actorId, DateTime nowUtc, CancellationToken ct);
    Task<int> UpdateAsync(Guid groupId, string name, string? description, bool isActive, Guid actorId, DateTime nowUtc, CancellationToken ct);
    Task UpsertMemberAsync(Guid groupId, Guid userId, bool isResponsible, Guid actorId, DateTime nowUtc, CancellationToken ct);
    Task<MembershipChangeResult> RemoveMemberAsync(Guid groupId, Guid userId, Guid actorId, DateTime nowUtc, CancellationToken ct);
    Task<IReadOnlyList<UserContact>> GetMemberContactsForObjectAsync(Guid objectId, CancellationToken ct);
}

public interface IAccessRequestRepository
{
    Task<string> InsertAsync(NewAccessRequest request, CancellationToken ct);
    Task<AccessRequestView?> GetAsync(Guid requestId, CancellationToken ct);
    Task<PagedResult<AccessRequestView>> SearchAsync(AccessRequestScope scope, Guid viewerId, bool viewerIsSecurity, RequestState? state,
        PageRequest page, CancellationToken ct);
    Task<IReadOnlyList<UserContact>> GetEligibleApproversAsync(Guid objectId, Guid requesterId, ApproverKind kind, CancellationToken ct);
    Task<bool> ExistsPendingAsync(Guid requesterId, Guid objectId, AccessAction action, CancellationToken ct);
    Task DecideAsync(Guid requestId, Guid approverId, Decision decision, string? comment, DateTime nowUtc, Guid? accessId,
        DateTime? startUtc, DateTime? endUtc, CancellationToken ct);
    Task CancelAsync(Guid requestId, Guid requesterId, DateTime nowUtc, CancellationToken ct);
    Task<IReadOnlyList<ExpiredRequest>> ExpirePendingAsync(DateTime nowUtc, CancellationToken ct);
}

public interface ITemporaryAccessRepository
{
    Task<IReadOnlyList<TemporaryAccessView>> ListByUserAsync(Guid userId, bool onlyCurrent, CancellationToken ct);
    Task<TemporaryAccessRecord?> GetAsync(Guid accessId, CancellationToken ct);
    Task RevokeAsync(Guid accessId, Guid actorId, string reason, DateTime nowUtc, CancellationToken ct);
    Task<int> RevokeAllForUserAsync(Guid userId, Guid actorId, string reason, DateTime nowUtc, CancellationToken ct);
    Task<(int Activated, IReadOnlyList<ExpiredAccess> Expired)> ProcessWindowAsync(DateTime nowUtc, CancellationToken ct);
}

public interface IAlertRepository
{
    Task<PagedResult<AlertView>> SearchAsync(AlertSearchCriteria criteria, PageRequest page, VisibilityScope scope, CancellationToken ct);
    Task<AlertRecord?> GetAsync(Guid alertId, CancellationToken ct);
    Task<bool> InsertIfNotExistsAsync(Guid alertId, Guid objectId, AlertCandidate candidate, DateTime nowUtc, CancellationToken ct);
    Task AcknowledgeAsync(Guid alertId, Guid userId, string comment, DateTime nowUtc, CancellationToken ct);
    Task<bool> EscalateAsync(Guid alertId, byte newLevel, DateTime nowUtc, CancellationToken ct);
    Task<IReadOnlyList<AlertRecord>> GetEscalationCandidatesAsync(DateTime nowUtc, CancellationToken ct);
    Task<IReadOnlyList<UserContact>> GetEscalationRecipientsAsync(Guid objectId, byte level, CancellationToken ct);
}

public interface IExpirationRepository
{
    Task<IReadOnlyList<ExpirationPolicyView>> GetPoliciesAsync(CancellationToken ct);
    Task UpsertPolicyAsync(Guid policyId, string name, ObjectType? type, Criticality? criticality, string thresholds, bool isActive,
        Guid actorId, DateTime nowUtc, CancellationToken ct);
    Task<IReadOnlyList<MonitoringCandidate>> GetMonitoringCandidatesAsync(DateTime nowUtc, int horizonDays, CancellationToken ct);
}

public interface IAuditTrail
{
    Task AppendAsync(AuditEntry entry, CancellationToken ct);
    Task AppendUsageAsync(UsageEntry entry, CancellationToken ct);
}

public interface IAuditReader
{
    Task<PagedResult<AuditEventView>> SearchAsync(AuditSearchCriteria criteria, PageRequest page, CancellationToken ct);
    Task<IReadOnlyList<AuditEventView>> ListByResourceAsync(string resourceType, string resourceId, CancellationToken ct);
    Task<IReadOnlyList<UsageEventView>> ListUsageByObjectAsync(Guid objectId, CancellationToken ct);
    Task<AuditIntegrityStatus> VerifyChainAsync(DateTime nowUtc, CancellationToken ct);
}

public interface IUserDirectory
{
    Task<UserView?> GetAsync(Guid userId, CancellationToken ct);
    Task<PagedResult<UserView>> SearchAsync(string? text, string? role, bool? isActive, PageRequest page, CancellationToken ct);
    Task<IReadOnlyList<UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
    Task<IReadOnlyList<UserContact>> GetActiveByRoleAsync(string role, CancellationToken ct);
    Task<IReadOnlyList<UserLookup>> LookupAsync(string? text, int top, CancellationToken ct);

    /// <summary>Pertenencias a grupos y objetos en propiedad: impiden asignar Auditor o Seguridad (RN-103, §4.3).</summary>
    Task<(int GroupMemberships, int OwnedObjects)> GetAssignmentConstraintsAsync(Guid userId, CancellationToken ct);
}

public interface IAreaRepository
{
    Task<IReadOnlyList<AreaView>> GetAllAsync(CancellationToken ct);
    Task InsertAsync(Guid areaId, string code, string name, CancellationToken ct);
}

public interface IDashboardReader
{
    Task<DashboardSummary> GetAsync(VisibilityScope scope, DateTime nowUtc, CancellationToken ct);
}

public interface IJobRunRepository
{
    Task<long> StartAsync(string jobName, DateTime nowUtc, CancellationToken ct);
    Task FinishAsync(long runId, string status, int itemsProcessed, string? detail, DateTime nowUtc, CancellationToken ct);
    Task<IReadOnlyList<JobRunView>> ListRecentAsync(string? jobName, int top, CancellationToken ct);
}

/// <summary>Bloqueo distribuido de trabajos en segundo plano (sp_getapplock, IMP-13).</summary>
public interface IJobLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(string jobName, CancellationToken ct);
}
