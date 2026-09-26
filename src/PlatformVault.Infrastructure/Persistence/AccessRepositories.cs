using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Access;
using PlatformVault.Application.Groups;
using PlatformVault.Domain.Access;
using PlatformVault.Domain.Objects;
using static PlatformVault.Infrastructure.Persistence.DbValues;

namespace PlatformVault.Infrastructure.Persistence;

public sealed class SecretPayloadStore(StoredProcedures sp) : ISecretPayloadStore
{
    public Task InsertAsync(EncryptedPayload payload, Guid actorId, CancellationToken ct) =>
        sp.ExecuteAsync("vault.usp_SecretPayload_Insert", new
        {
            payload.ObjectId,
            payload.VersionNumber,
            payload.Component,
            PayloadKind = payload.PayloadKind.ToString(),
            payload.Ciphertext,
            payload.Nonce,
            payload.Tag,
            payload.WrappedDek,
            payload.KekThumbprint,
            payload.Algorithm,
            CreatedBy = actorId,
        }, ct);

    public async Task<EncryptedPayload?> GetLatestAsync(Guid objectId, byte component, CancellationToken ct)
    {
        var row = await sp.QuerySingleOrDefaultAsync<PayloadRow>("vault.usp_SecretPayload_GetLatest",
            new { ObjectId = objectId, Component = component }, ct);
        return row is null
            ? null
            : new EncryptedPayload(row.ObjectId, row.VersionNumber, row.Component, Enum<PayloadKind>(row.PayloadKind), row.Ciphertext,
                row.Nonce, row.Tag, row.WrappedDek, row.KekThumbprint, row.Algorithm);
    }

    private sealed class PayloadRow
    {
        public Guid ObjectId { get; set; }
        public int VersionNumber { get; set; }
        public byte Component { get; set; }
        public string PayloadKind { get; set; } = "";
        public byte[] Ciphertext { get; set; } = [];
        public byte[] Nonce { get; set; } = [];
        public byte[] Tag { get; set; } = [];
        public byte[] WrappedDek { get; set; } = [];
        public string KekThumbprint { get; set; } = "";
        public string Algorithm { get; set; } = "";
    }
}

public sealed class GroupRepository(StoredProcedures sp) : IGroupRepository
{
    public Task<GroupDetail?> GetAsync(Guid groupId, Guid viewerId, bool hasGlobalScope, CancellationToken ct) =>
        sp.QueryMultipleAsync("app.usp_Group_GetById", new { GroupId = groupId, ViewerUserId = viewerId, HasGlobalScope = hasGlobalScope },
            async grid =>
            {
                if (grid.IsConsumed) return null;
                var group = await grid.ReadFirstOrDefaultAsync<GroupRow>();
                var members = (await grid.ReadAsync<MemberRow>()).Select(m => new GroupMemberView(m.UserId, m.UserName, m.DisplayName,
                    m.IsActive, m.IsResponsible, Utc(m.AddedAtUtc))).ToList();
                return group is null
                    ? null
                    : new GroupDetail(group.GroupId, group.Code, group.Name, group.Description, group.AreaId, group.IsActive, group.ObjectCount,
                        Utc(group.CreatedAtUtc), members);
            }, ct);

    public async Task<PagedResult<GroupSummary>> SearchAsync(string? text, bool? isActive, PageRequest page, Guid viewerId, bool hasGlobalScope,
        CancellationToken ct)
    {
        var rows = await sp.QueryAsync<GroupRow>("app.usp_Group_Search", new
        {
            ViewerUserId = viewerId,
            HasGlobalScope = hasGlobalScope,
            Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            IsActive = isActive,
            page.Offset,
            PageSize = page.SafePageSize,
        }, ct);
        return new PagedResult<GroupSummary>(
            rows.Select(r => new GroupSummary(r.GroupId, r.Code, r.Name, r.Description, r.AreaId, r.IsActive, r.MemberCount, r.ObjectCount)).ToList(),
            page.SafePage, page.SafePageSize, rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    public async Task<IReadOnlyList<GroupCapacity>> GetCapacitiesAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<GroupRow>("app.usp_Group_GetManyByIds", new { GroupIds = StoredProcedures.GuidList(groupIds) }, ct);
        return rows.Select(r => new GroupCapacity(r.GroupId, r.Code, r.Name, r.IsActive, r.ActiveMemberCount)).ToList();
    }

    public async Task<string> InsertAsync(Guid groupId, string name, string? description, Guid? areaId, Guid responsibleUserId, Guid actorId,
        DateTime nowUtc, CancellationToken ct) =>
        await sp.QuerySingleOrDefaultAsync<string>("app.usp_Group_Insert", new
        {
            GroupId = groupId,
            Name = name,
            Description = description,
            AreaId = areaId,
            ResponsibleUserId = responsibleUserId,
            CreatedBy = actorId,
            NowUtc = nowUtc,
        }, ct) ?? string.Empty;

    public async Task<int> UpdateAsync(Guid groupId, string name, string? description, bool isActive, Guid actorId, DateTime nowUtc,
        CancellationToken ct) =>
        await sp.QuerySingleOrDefaultAsync<int>("app.usp_Group_Update", new
        {
            GroupId = groupId,
            Name = name,
            Description = description,
            IsActive = isActive,
            ModifiedBy = actorId,
            NowUtc = nowUtc,
        }, ct);

    public Task UpsertMemberAsync(Guid groupId, Guid userId, bool isResponsible, Guid actorId, DateTime nowUtc, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_GroupMember_Upsert",
            new { GroupId = groupId, UserId = userId, IsResponsible = isResponsible, AddedBy = actorId, NowUtc = nowUtc }, ct);

    public async Task<MembershipChangeResult> RemoveMemberAsync(Guid groupId, Guid userId, Guid actorId, DateTime nowUtc, CancellationToken ct)
    {
        var row = await sp.QuerySingleOrDefaultAsync<ChangeRow>("app.usp_GroupMember_Delete",
            new { GroupId = groupId, UserId = userId, RemovedBy = actorId, NowUtc = nowUtc }, ct);
        return new MembershipChangeResult(row?.RevokedAccesses ?? 0, row?.CancelledRequests ?? 0);
    }

    public async Task<IReadOnlyList<UserContact>> GetMemberContactsForObjectAsync(Guid objectId, CancellationToken ct) =>
        await sp.ContactsAsync("app.usp_GroupMember_GetContactsForObject", new { ObjectId = objectId }, ct);

    private sealed class GroupRow
    {
        public Guid GroupId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public Guid? AreaId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int ObjectCount { get; set; }
        public int MemberCount { get; set; }
        public int ActiveMemberCount { get; set; }
        public int TotalCount { get; set; }
    }

    private sealed class MemberRow
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsActive { get; set; }
        public bool IsResponsible { get; set; }
        public DateTime AddedAtUtc { get; set; }
    }

    private sealed class ChangeRow
    {
        public int RevokedAccesses { get; set; }
        public int CancelledRequests { get; set; }
    }
}

public sealed class AccessRequestRepository(StoredProcedures sp) : IAccessRequestRepository
{
    public async Task<string> InsertAsync(NewAccessRequest request, CancellationToken ct) =>
        await sp.QuerySingleOrDefaultAsync<string>("app.usp_AccessRequest_Insert", new
        {
            request.RequestId,
            request.RequesterId,
            request.ObjectId,
            Action = request.Request.Action.ToString(),
            request.Request.Justification,
            RequestedStartUtc = request.Request.StartUtc,
            request.Request.DurationMinutes,
            ApproverKind = request.Request.ApproverKind.ToString(),
            request.NowUtc,
            PendingExpiresAtUtc = request.Request.PendingExpiresAtUtc,
        }, ct) ?? string.Empty;

    public Task<AccessRequestView?> GetAsync(Guid requestId, CancellationToken ct) =>
        sp.QueryMultipleAsync("app.usp_AccessRequest_GetById", new { RequestId = requestId }, async grid =>
        {
            var row = await grid.ReadFirstOrDefaultAsync<RequestRow>();
            var decisions = (await grid.ReadAsync<DecisionRow>()).Select(d => new ApprovalDecisionView(d.ApproverId, d.ApproverName,
                Enum<Decision>(d.Decision), d.Comment, Utc(d.DecidedAtUtc))).ToList();
            return row is null ? null : ToView(row) with { Decisions = decisions };
        }, ct);

    public async Task<PagedResult<AccessRequestView>> SearchAsync(AccessRequestScope scope, Guid viewerId, bool viewerIsSecurity,
        RequestState? state, PageRequest page, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<RequestRow>("app.usp_AccessRequest_Search", new
        {
            Scope = scope.ToString(),
            ViewerUserId = viewerId,
            ViewerIsSecurity = viewerIsSecurity,
            State = state?.ToString(),
            page.Offset,
            PageSize = page.SafePageSize,
        }, ct);
        return new PagedResult<AccessRequestView>(rows.Select(ToView).ToList(), page.SafePage, page.SafePageSize,
            rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    public async Task<IReadOnlyList<UserContact>> GetEligibleApproversAsync(Guid objectId, Guid requesterId, ApproverKind kind, CancellationToken ct) =>
        await sp.ContactsAsync("app.usp_AccessRequest_GetEligibleApprovers",
            new { ObjectId = objectId, RequesterId = requesterId, ApproverKind = kind.ToString() }, ct);

    public async Task<bool> ExistsPendingAsync(Guid requesterId, Guid objectId, AccessAction action, CancellationToken ct) =>
        await sp.QuerySingleOrDefaultAsync<bool>("app.usp_AccessRequest_ExistsPending",
            new { RequesterId = requesterId, ObjectId = objectId, Action = action.ToString() }, ct);

    public Task DecideAsync(Guid requestId, Guid approverId, Decision decision, string? comment, DateTime nowUtc, Guid? accessId,
        DateTime? startUtc, DateTime? endUtc, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_AccessRequest_Decide", new
        {
            RequestId = requestId,
            ApproverId = approverId,
            Decision = decision.ToString(),
            Comment = comment,
            NowUtc = nowUtc,
            AccessId = accessId,
            StartUtc = startUtc,
            EndUtc = endUtc,
        }, ct);

    public Task CancelAsync(Guid requestId, Guid requesterId, DateTime nowUtc, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_AccessRequest_Cancel", new { RequestId = requestId, RequesterId = requesterId, NowUtc = nowUtc }, ct);

    public async Task<IReadOnlyList<ExpiredRequest>> ExpirePendingAsync(DateTime nowUtc, CancellationToken ct) =>
        (await sp.QueryAsync<ExpiredRequestRow>("app.usp_AccessRequest_ExpirePending", new { NowUtc = nowUtc }, ct))
            .Select(r => new ExpiredRequest(r.RequestId, r.Code, r.RequesterId, r.ObjectId)).ToList();

    private static AccessRequestView ToView(RequestRow r) => new(r.RequestId, r.Code, r.RequesterId, r.RequesterName, r.ObjectId, r.ObjectCode,
        r.ObjectName, Enum<Criticality>(r.Criticality), Enum<Sensitivity>(r.Sensitivity), Enum<AccessAction>(r.Action), r.Justification,
        Utc(r.RequestedStartUtc), r.DurationMinutes, Enum<ApproverKind>(r.ApproverKind), Enum<RequestState>(r.State), Utc(r.CreatedAtUtc),
        Utc(r.PendingExpiresAtUtc), Utc(r.DecidedAtUtc));

    private sealed class RequestRow
    {
        public Guid RequestId { get; set; }
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
        public DateTime RequestedStartUtc { get; set; }
        public int DurationMinutes { get; set; }
        public string ApproverKind { get; set; } = "";
        public string State { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
        public DateTime PendingExpiresAtUtc { get; set; }
        public DateTime? DecidedAtUtc { get; set; }
        public int TotalCount { get; set; }
    }

    private sealed class ExpiredRequestRow
    {
        public Guid RequestId { get; set; }
        public string Code { get; set; } = "";
        public Guid RequesterId { get; set; }
        public Guid ObjectId { get; set; }
    }

    private sealed class DecisionRow
    {
        public Guid ApproverId { get; set; }
        public string ApproverName { get; set; } = "";
        public string Decision { get; set; } = "";
        public string? Comment { get; set; }
        public DateTime DecidedAtUtc { get; set; }
    }
}

public sealed class TemporaryAccessRepository(StoredProcedures sp) : ITemporaryAccessRepository
{
    public async Task<IReadOnlyList<TemporaryAccessView>> ListByUserAsync(Guid userId, bool onlyCurrent, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<AccessRow>("app.usp_TemporaryAccess_ListByUser", new { UserId = userId, OnlyCurrent = onlyCurrent }, ct);
        return rows.Select(r => new TemporaryAccessView(r.AccessId, r.RequestId, r.ObjectId, r.ObjectCode, r.ObjectName, Enum<AccessAction>(r.Action),
            Utc(r.StartUtc), Utc(r.EndUtc), Enum<TemporaryAccessState>(r.State), Utc(r.RevokedAtUtc), r.RevokeReason)).ToList();
    }

    public async Task<TemporaryAccessRecord?> GetAsync(Guid accessId, CancellationToken ct)
    {
        var r = await sp.QuerySingleOrDefaultAsync<AccessRow>("app.usp_TemporaryAccess_GetById", new { AccessId = accessId }, ct);
        return r is null
            ? null
            : new TemporaryAccessRecord(r.AccessId, r.RequestId, r.ObjectId, r.BeneficiaryId, Enum<AccessAction>(r.Action), Utc(r.StartUtc),
                Utc(r.EndUtc), Enum<TemporaryAccessState>(r.State));
    }

    public Task RevokeAsync(Guid accessId, Guid actorId, string reason, DateTime nowUtc, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_TemporaryAccess_Revoke", new { AccessId = accessId, RevokedBy = actorId, Reason = reason, NowUtc = nowUtc }, ct);

    public async Task<int> RevokeAllForUserAsync(Guid userId, Guid actorId, string reason, DateTime nowUtc, CancellationToken ct) =>
        await sp.QuerySingleOrDefaultAsync<int>("app.usp_TemporaryAccess_RevokeAllForUser",
            new { UserId = userId, RevokedBy = actorId, Reason = reason, NowUtc = nowUtc }, ct);

    public Task<(int Activated, IReadOnlyList<ExpiredAccess> Expired)> ProcessWindowAsync(DateTime nowUtc, CancellationToken ct) =>
        sp.QueryMultipleAsync("app.usp_TemporaryAccess_ProcessWindow", new { NowUtc = nowUtc }, async grid =>
        {
            var activated = await grid.ReadFirstAsync<int>();
            IReadOnlyList<ExpiredAccess> expired = (await grid.ReadAsync<AccessRow>()).Select(a => new ExpiredAccess(a.AccessId, a.ObjectId, a.BeneficiaryId)).ToList();
            return (activated, expired);
        }, ct);

    private sealed class AccessRow
    {
        public Guid AccessId { get; set; }
        public Guid RequestId { get; set; }
        public Guid ObjectId { get; set; }
        public string ObjectCode { get; set; } = "";
        public string ObjectName { get; set; } = "";
        public Guid BeneficiaryId { get; set; }
        public string Action { get; set; } = "";
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string State { get; set; } = "";
        public DateTime? RevokedAtUtc { get; set; }
        public string? RevokeReason { get; set; }
    }
}
