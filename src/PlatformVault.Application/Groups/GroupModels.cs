namespace PlatformVault.Application.Groups;

public sealed record GroupSummary(Guid Id, string Code, string Name, string? Description, Guid? AreaId, bool IsActive, int MemberCount, int ObjectCount);

public sealed record GroupDetail(Guid Id, string Code, string Name, string? Description, Guid? AreaId, bool IsActive, int ObjectCount,
    DateTime CreatedAt, IReadOnlyList<GroupMemberView> Members);

public sealed record GroupMemberView(Guid UserId, string UserName, string DisplayName, bool IsActive, bool IsResponsible, DateTime AddedAt);

/// <summary>Grupo candidato a asignarse a un objeto.</summary>
public sealed record GroupCapacity(Guid GroupId, string Code, string Name, bool IsActive, int ActiveMemberCount);

public sealed record MembershipChangeResult(int RevokedAccesses, int CancelledRequests);
