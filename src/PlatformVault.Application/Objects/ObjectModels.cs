using PlatformVault.Domain.Access;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Objects;

/// <summary>Datos mínimos para decidir la autorización por objeto (IMP-08). Nunca se devuelve al cliente.</summary>
public sealed record ObjectAuthorizationContext(
    Guid ObjectId,
    string Code,
    ObjectType ObjectType,
    Criticality Criticality,
    Sensitivity Sensitivity,
    LifecycleState LifecycleState,
    CustodyMode CustodyMode,
    bool HasPayload,
    Guid AreaId,
    Guid? OwnerId,
    Guid CreatedBy,
    int CurrentVersion,
    IReadOnlyList<ObjectGroupInfo> Groups,
    IReadOnlyList<ActiveAccessInfo> ActiveAccesses)
{
    public bool IsOwner(Guid userId) => OwnerId == userId;

    public bool IsCreator(Guid userId) => CreatedBy == userId;

    public bool IsGroupMember => Groups.Any(g => g.IsActive && g.ViewerIsMember);

    public int MaxActiveMembersInAGroup => Groups.Where(g => g.IsActive).Select(g => g.ActiveMemberCount).DefaultIfEmpty(0).Max();
}

public sealed record ObjectGroupInfo(Guid GroupId, string Code, string Name, bool IsActive, int ActiveMemberCount, bool ViewerIsMember);

public sealed record ActiveAccessInfo(Guid AccessId, AccessAction Action, DateTime StartUtc, DateTime EndUtc);

/// <summary>Objeto tal como se lista (contrato: ManagedObject). No contiene nunca el valor sensible.</summary>
public record ObjectSummary
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public ObjectType Type { get; init; }
    public string Subtype { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Criticality Criticality { get; init; }
    public Sensitivity Sensitivity { get; init; }
    public DeploymentEnvironment Environment { get; init; }
    public Guid AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public Guid? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public LifecycleState LifecycleState { get; init; }
    public ExpirationStatus ExpirationStatus { get; init; }
    public DateTime? ExpirationDate { get; init; }
    public bool NoExpirationJustified { get; init; }
    public CustodyMode CustodyMode { get; init; }
    public bool HasValue { get; init; }
    public int CurrentVersion { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid CreatedBy { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public Guid? ModifiedBy { get; init; }

    /// <summary>Versión de fila para concurrencia optimista (cabecera If-Match).</summary>
    public string ETag { get; init; } = string.Empty;
}

/// <summary>Detalle de un objeto (contrato: ManagedObjectDetail).</summary>
public sealed record ObjectDetail : ObjectSummary
{
    public string? ThumbprintSha256 { get; init; }
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<ObjectGroupRef> Groups { get; init; } = [];
}

public sealed record ObjectGroupRef(Guid Id, string Code, string Name, bool IsActive);

public sealed record ObjectSearchCriteria
{
    public string? Text { get; init; }
    public ObjectType? Type { get; init; }
    public Criticality? Criticality { get; init; }
    public Sensitivity? Sensitivity { get; init; }
    public DeploymentEnvironment? Environment { get; init; }
    public LifecycleState? LifecycleState { get; init; }
    public Guid? AreaId { get; init; }
    public Guid? OwnerId { get; init; }
    public ExpirationStatus? ExpirationStatus { get; init; }
    public bool WithoutOwner { get; init; }
    public DateTime? ExpiresBefore { get; init; }
    public DateTime? ExpiresAfter { get; init; }
    public string? Subtype { get; init; }
    public Guid? GroupId { get; init; }
    public ObjectSortField SortBy { get; init; } = ObjectSortField.Name;
    public bool SortDescending { get; init; }
}

public enum ObjectSortField
{
    Name,
    Code,
    ExpirationDate,
    CreatedAt,
    Criticality,
}

public sealed record ObjectVersionEntry(int Version, Guid ChangedBy, string? ChangedByName, DateTime ChangedAt, string Reason, IReadOnlyList<string> ChangedFields);

public sealed record OwnershipChange(OwnerRole Role, Guid? PreviousUserId, string? PreviousUserName, Guid NewUserId, string? NewUserName,
    Guid ChangedBy, string? ChangedByName, DateTime ChangedAt, string Reason);

public sealed record ObjectWriteResult(string ETag, int CurrentVersion, string? Code = null);

public sealed record StateChangeResult(string ETag, int CurrentVersion, int RevokedAccesses, int CancelledRequests);

/// <summary>Información extraída de un archivo de certificado (RN-016).</summary>
public sealed record CertificateInfo(
    string Subject,
    string Issuer,
    string SerialNumber,
    IReadOnlyList<string> SubjectAlternativeNames,
    DateTime NotBeforeUtc,
    DateTime NotAfterUtc,
    string ThumbprintSha256,
    string SignatureAlgorithm,
    string KeyAlgorithm,
    int KeySize,
    bool HasPrivateKey,
    byte[] CertificateDer);
