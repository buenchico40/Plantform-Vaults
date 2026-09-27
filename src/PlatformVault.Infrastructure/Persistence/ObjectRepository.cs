using System.Text.Json;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Common;
using PlatformVault.Application.Objects;
using PlatformVault.Domain.Access;
using PlatformVault.Domain.Objects;
using static PlatformVault.Infrastructure.Persistence.DbValues;

namespace PlatformVault.Infrastructure.Persistence;

public sealed class ObjectRepository(StoredProcedures sp) : IObjectRepository
{
    private const int ExpiringSoonDays = ManagedObject.ExpiringSoonDaysDefault;

    public Task<ObjectAuthorizationContext?> GetAuthorizationContextAsync(Guid objectId, Guid viewerId, DateTime nowUtc, CancellationToken ct) =>
        sp.QueryMultipleAsync("app.usp_ManagedObject_GetAuthorizationContext", new { ObjectId = objectId, ViewerUserId = viewerId, NowUtc = nowUtc },
            async grid =>
            {
                var row = await grid.ReadFirstOrDefaultAsync<AuthRow>();
                var groups = (await grid.ReadAsync<GroupRow>()).Select(g => new ObjectGroupInfo(g.GroupId, g.Code, g.Name, g.IsActive,
                    g.ActiveMemberCount, g.ViewerIsMember)).ToList();
                var accesses = (await grid.ReadAsync<AccessRow>()).Select(a => new ActiveAccessInfo(a.AccessId, Enum<AccessAction>(a.Action),
                    Utc(a.StartUtc), Utc(a.EndUtc))).ToList();
                return row is null
                    ? null
                    : new ObjectAuthorizationContext(row.ObjectId, row.Code, Enum<ObjectType>(row.ObjectType), Enum<Criticality>(row.Criticality),
                        Enum<Sensitivity>(row.Sensitivity), Enum<LifecycleState>(row.LifecycleState), Enum<CustodyMode>(row.CustodyMode),
                        row.HasPayload, row.AreaId, row.OwnerId, row.CreatedBy, row.CurrentVersion, groups, accesses);
            }, ct);

    public async Task<ManagedObject?> LoadAsync(Guid objectId, CancellationToken ct)
    {
        var row = await sp.QueryMultipleAsync("app.usp_ManagedObject_GetById", GetByIdParameters(objectId, Guid.Empty, true, DateTime.UtcNow),
            grid => grid.ReadFirstOrDefaultAsync<ObjectRow>(), ct);
        if (row is null) return null;
        return new ManagedObject
        {
            ObjectId = row.ObjectId,
            Code = row.Code,
            ObjectType = Enum<ObjectType>(row.ObjectType),
            Subtype = row.Subtype,
            Name = row.Name,
            Description = row.Description,
            Criticality = Enum<Criticality>(row.Criticality),
            Sensitivity = Enum<Sensitivity>(row.Sensitivity),
            Environment = Enum<DeploymentEnvironment>(row.Environment),
            AreaId = row.AreaId,
            OwnerId = row.OwnerId,
            LifecycleState = Enum<LifecycleState>(row.LifecycleState),
            CustodyMode = Enum<CustodyMode>(row.CustodyMode),
            HasPayload = row.HasPayload,
            ExpirationDate = Utc(row.ExpirationDate),
            NoExpirationJustified = row.NoExpirationJustified,
            Thumbprint = row.Thumbprint,
            CurrentVersion = row.CurrentVersion,
            RowVer = row.RowVer,
            Details = ParseDetails(row.DetailsJson),
        };
    }

    public Task<ObjectDetail?> GetDetailAsync(Guid objectId, VisibilityScope scope, DateTime nowUtc, CancellationToken ct) =>
        sp.QueryMultipleAsync("app.usp_ManagedObject_GetById",
            GetByIdParameters(objectId, scope.ViewerUserId, scope.HasGlobalScope, nowUtc),
            async grid =>
            {
                var row = await grid.ReadFirstOrDefaultAsync<ObjectRow>();
                var groups = (await grid.ReadAsync<GroupRow>()).Select(g => new ObjectGroupRef(g.GroupId, g.Code, g.Name, g.IsActive)).ToList();
                if (row is null) return null;
                return new ObjectDetail
                {
                    Id = row.ObjectId,
                    Code = row.Code,
                    Type = Enum<ObjectType>(row.ObjectType),
                    Subtype = row.Subtype,
                    Name = row.Name,
                    Description = row.Description,
                    Criticality = Enum<Criticality>(row.Criticality),
                    Sensitivity = Enum<Sensitivity>(row.Sensitivity),
                    Environment = Enum<DeploymentEnvironment>(row.Environment),
                    AreaId = row.AreaId,
                    AreaName = row.AreaName,
                    OwnerId = row.OwnerId,
                    OwnerName = row.OwnerName,
                    LifecycleState = Enum<LifecycleState>(row.LifecycleState),
                    ExpirationStatus = Enum<ExpirationStatus>(row.ExpirationStatus),
                    ExpirationDate = Utc(row.ExpirationDate),
                    NoExpirationJustified = row.NoExpirationJustified,
                    CustodyMode = Enum<CustodyMode>(row.CustodyMode),
                    HasValue = row.HasPayload,
                    CurrentVersion = row.CurrentVersion,
                    CreatedAt = Utc(row.CreatedAtUtc),
                    CreatedBy = row.CreatedBy,
                    ModifiedAt = Utc(row.ModifiedAtUtc),
                    ModifiedBy = row.ModifiedBy,
                    ETag = ETag.From(row.RowVer),
                    ThumbprintSha256 = row.Thumbprint,
                    Attributes = ParseDetails(row.DetailsJson),
                    Groups = groups,
                };
            }, ct);

    public async Task<PagedResult<ObjectSummary>> SearchAsync(ObjectSearchCriteria criteria, PageRequest page, VisibilityScope scope,
        DateTime nowUtc, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<ObjectRow>("app.usp_ManagedObject_Search", new
        {
            scope.ViewerUserId,
            scope.HasGlobalScope,
            NowUtc = nowUtc,
            ExpiringSoonDays,
            criteria.Text,
            ObjectType = criteria.Type?.ToString(),
            Criticality = criteria.Criticality?.ToString(),
            Sensitivity = criteria.Sensitivity?.ToString(),
            Environment = criteria.Environment?.ToString(),
            LifecycleState = criteria.LifecycleState?.ToString(),
            criteria.AreaId,
            criteria.OwnerId,
            ExpirationStatus = criteria.ExpirationStatus?.ToString(),
            criteria.WithoutOwner,
            ExpiresBeforeUtc = criteria.ExpiresBefore,
            SortBy = criteria.SortBy.ToString(),
            criteria.SortDescending,
            page.Offset,
            PageSize = page.SafePageSize,
            criteria.Subtype,
            criteria.GroupId,
            ExpiresAfterUtc = criteria.ExpiresAfter,
        }, ct);
        var items = rows.Select(ToSummary).ToList();
        return new PagedResult<ObjectSummary>(items, page.SafePage, page.SafePageSize, rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    public async Task<(bool NameExists, bool ThumbprintExists)> ExistsDuplicateAsync(ObjectType type, DeploymentEnvironment environment,
        Guid areaId, string name, string? thumbprint, Guid? excludeObjectId, CancellationToken ct)
    {
        var row = await sp.QuerySingleOrDefaultAsync<DuplicateRow>("app.usp_ManagedObject_ExistsDuplicate", new
        {
            ObjectType = type.ToString(),
            Environment = environment.ToString(),
            AreaId = areaId,
            Name = name,
            Thumbprint = thumbprint,
            ExcludeObjectId = excludeObjectId,
        }, ct);
        return (row?.NameExists ?? false, row?.ThumbprintExists ?? false);
    }

    public async Task<ObjectWriteResult> InsertAsync(ManagedObject obj, Guid actorId, DateTime nowUtc, string reason, string changedFieldsJson,
        IReadOnlyCollection<Guid> groupIds, CancellationToken ct)
    {
        var row = await sp.QuerySingleOrDefaultAsync<WriteRow>("app.usp_ManagedObject_Insert", new
        {
            obj.ObjectId,
            ObjectType = obj.ObjectType.ToString(),
            obj.Subtype,
            obj.Name,
            obj.Description,
            Criticality = obj.Criticality.ToString(),
            Sensitivity = obj.Sensitivity.ToString(),
            Environment = obj.Environment.ToString(),
            obj.AreaId,
            obj.OwnerId,
            CustodyMode = obj.CustodyMode.ToString(),
            obj.HasPayload,
            obj.ExpirationDate,
            obj.NoExpirationJustified,
            obj.Thumbprint,
            DetailsJson = SerializeDetails(obj.Details),
            ModifiedBy = actorId,
            NowUtc = nowUtc,
            Reason = reason,
            ChangedFieldsJson = changedFieldsJson,
            GroupIds = StoredProcedures.GuidList(groupIds),
        }, ct) ?? throw new InvalidOperationException("El alta no devolvió resultado.");
        obj.Code = row.Code ?? string.Empty;
        obj.RowVer = row.RowVer;
        return new ObjectWriteResult(ETag.From(row.RowVer), row.CurrentVersion, row.Code);
    }

    public Task<ObjectWriteResult> UpdateMetadataAsync(ManagedObject obj, byte[] expectedRowVer, Guid actorId, DateTime nowUtc, string reason,
        string changedFieldsJson, bool resolveOpenAlerts, CancellationToken ct) =>
        WriteAsync("app.usp_ManagedObject_UpdateMetadata", new
        {
            obj.ObjectId,
            obj.Subtype,
            obj.Name,
            obj.Description,
            Criticality = obj.Criticality.ToString(),
            Sensitivity = obj.Sensitivity.ToString(),
            Environment = obj.Environment.ToString(),
            obj.ExpirationDate,
            obj.NoExpirationJustified,
            DetailsJson = SerializeDetails(obj.Details),
            ExpectedRowVer = expectedRowVer,
            ModifiedBy = actorId,
            NowUtc = nowUtc,
            Reason = reason,
            ChangedFieldsJson = changedFieldsJson,
            ResolveOpenAlerts = resolveOpenAlerts,
        }, ct);

    public async Task<StateChangeResult> ChangeStateAsync(Guid objectId, LifecycleState newState, byte[] expectedRowVer, Guid actorId,
        DateTime nowUtc, string reason, CancellationToken ct)
    {
        var row = await sp.QuerySingleOrDefaultAsync<WriteRow>("app.usp_ManagedObject_ChangeState", new
        {
            ObjectId = objectId,
            NewState = newState.ToString(),
            ExpectedRowVer = expectedRowVer,
            ModifiedBy = actorId,
            NowUtc = nowUtc,
            Reason = reason,
            ChangedFieldsJson = "[\"lifecycleState\"]",
        }, ct) ?? throw new ConcurrencyFailure();
        return new StateChangeResult(ETag.From(row.RowVer), row.CurrentVersion, row.RevokedAccesses, row.CancelledRequests);
    }

    public Task<ObjectWriteResult> SetOwnerAsync(Guid objectId, Guid ownerId, byte[] expectedRowVer,
        Guid actorId, DateTime nowUtc, string reason, CancellationToken ct) =>
        WriteAsync("app.usp_ManagedObject_SetOwner", new
        {
            ObjectId = objectId,
            OwnerId = ownerId,
            ExpectedRowVer = expectedRowVer,
            ModifiedBy = actorId,
            NowUtc = nowUtc,
            Reason = reason,
            ChangedFieldsJson = "[\"owner\"]",
        }, ct);

    public Task<ObjectWriteResult> SetGroupsAsync(Guid objectId, IReadOnlyCollection<Guid> groupIds, byte[] expectedRowVer, Guid actorId,
        DateTime nowUtc, string reason, CancellationToken ct) =>
        WriteAsync("app.usp_ManagedObject_SetGroups", new
        {
            ObjectId = objectId,
            GroupIds = StoredProcedures.GuidList(groupIds),
            ExpectedRowVer = expectedRowVer,
            ModifiedBy = actorId,
            NowUtc = nowUtc,
            Reason = reason,
            ChangedFieldsJson = "[\"groups\"]",
        }, ct);

    public Task<ObjectWriteResult> BumpVersionForPayloadAsync(ManagedObject obj, byte[] expectedRowVer, Guid actorId, DateTime nowUtc,
        string reason, bool updateExpiration, bool updateCertificate, CancellationToken ct) =>
        WriteAsync("app.usp_ManagedObject_BumpVersionForPayload", new
        {
            obj.ObjectId,
            ExpectedRowVer = expectedRowVer,
            ModifiedBy = actorId,
            NowUtc = nowUtc,
            Reason = reason,
            ChangedFieldsJson = updateCertificate ? "[\"value\",\"certificate\"]" : updateExpiration ? "[\"value\",\"expirationDate\"]" : "[\"value\"]",
            UpdateExpiration = updateExpiration,
            obj.ExpirationDate,
            UpdateCertificate = updateCertificate,
            obj.Thumbprint,
            DetailsJson = SerializeDetails(obj.Details),
        }, ct);

    public Task InsertPublicCertificateAsync(Guid objectId, int versionNumber, byte[] certificateDer, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_ObjectCertificate_Insert",
            new { ObjectId = objectId, VersionNumber = versionNumber, CertificateDer = certificateDer }, ct);

    public async Task<byte[]?> GetPublicCertificateAsync(Guid objectId, CancellationToken ct)
    {
        var row = await sp.QuerySingleOrDefaultAsync<CertificateRow>("app.usp_ObjectCertificate_GetLatest", new { ObjectId = objectId }, ct);
        return row?.CertificateDer;
    }

    public async Task<IReadOnlyList<ObjectVersionEntry>> ListVersionsAsync(Guid objectId, VisibilityScope scope, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<VersionRow>("app.usp_ObjectVersion_ListByObject",
            new { ObjectId = objectId, scope.ViewerUserId, scope.HasGlobalScope }, ct);
        return rows.Select(r => new ObjectVersionEntry(r.VersionNumber, r.ChangedBy, r.ChangedByName, Utc(r.ChangedAtUtc), r.Reason,
            ParseFields(r.ChangedFieldsJson))).ToList();
    }

    public async Task<IReadOnlyList<OwnershipChange>> ListOwnershipHistoryAsync(Guid objectId, VisibilityScope scope, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<OwnershipRow>("app.usp_OwnershipHistory_ListByObject",
            new { ObjectId = objectId, scope.ViewerUserId, scope.HasGlobalScope }, ct);
        return rows.Select(r => new OwnershipChange(Enum<OwnerRole>(r.OwnerRole), r.PreviousUserId, r.PreviousUserName, r.NewUserId,
            r.NewUserName, r.ChangedBy, r.ChangedByName, Utc(r.ChangedAtUtc), r.Reason)).ToList();
    }

    private async Task<ObjectWriteResult> WriteAsync(string procedure, object parameters, CancellationToken ct)
    {
        var row = await sp.QuerySingleOrDefaultAsync<WriteRow>(procedure, parameters, ct) ?? throw new ConcurrencyFailure();
        return new ObjectWriteResult(ETag.From(row.RowVer), row.CurrentVersion);
    }

    private static object GetByIdParameters(Guid objectId, Guid viewer, bool global, DateTime now) => new
    {
        ObjectId = objectId,
        ViewerUserId = viewer,
        HasGlobalScope = global,
        NowUtc = now,
        ExpiringSoonDays,
    };

    private static ObjectSummary ToSummary(ObjectRow row) => new()
    {
        Id = row.ObjectId,
        Code = row.Code,
        Type = Enum<ObjectType>(row.ObjectType),
        Subtype = row.Subtype,
        Name = row.Name,
        Description = row.Description,
        Criticality = Enum<Criticality>(row.Criticality),
        Sensitivity = Enum<Sensitivity>(row.Sensitivity),
        Environment = Enum<DeploymentEnvironment>(row.Environment),
        AreaId = row.AreaId,
        AreaName = row.AreaName,
        OwnerId = row.OwnerId,
        OwnerName = row.OwnerName,
        LifecycleState = Enum<LifecycleState>(row.LifecycleState),
        ExpirationStatus = Enum<ExpirationStatus>(row.ExpirationStatus),
        ExpirationDate = Utc(row.ExpirationDate),
        NoExpirationJustified = row.NoExpirationJustified,
        CustodyMode = Enum<CustodyMode>(row.CustodyMode),
        HasValue = row.HasPayload,
        CurrentVersion = row.CurrentVersion,
        CreatedAt = Utc(row.CreatedAtUtc),
        CreatedBy = row.CreatedBy,
        ModifiedAt = Utc(row.ModifiedAtUtc),
        ModifiedBy = row.ModifiedBy,
        ETag = ETag.From(row.RowVer),
    };

    internal static string? SerializeDetails(IReadOnlyDictionary<string, string> details) =>
        details.Count == 0 ? null : JsonSerializer.Serialize(details);

    private static Dictionary<string, string> ParseDetails(string? json) =>
        string.IsNullOrEmpty(json)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.Ordinal);

    private static IReadOnlyList<string> ParseFields(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];

    private sealed class AuthRow
    {
        public Guid ObjectId { get; set; }
        public string Code { get; set; } = "";
        public string ObjectType { get; set; } = "";
        public string Criticality { get; set; } = "";
        public string Sensitivity { get; set; } = "";
        public string LifecycleState { get; set; } = "";
        public string CustodyMode { get; set; } = "";
        public bool HasPayload { get; set; }
        public Guid AreaId { get; set; }
        public Guid? OwnerId { get; set; }
        public Guid CreatedBy { get; set; }
        public int CurrentVersion { get; set; }
    }

    private sealed class GroupRow
    {
        public Guid GroupId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public int ActiveMemberCount { get; set; }
        public bool ViewerIsMember { get; set; }
    }

    private sealed class AccessRow
    {
        public Guid AccessId { get; set; }
        public string Action { get; set; } = "";
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }

    private sealed class ObjectRow
    {
        public Guid ObjectId { get; set; }
        public string Code { get; set; } = "";
        public string ObjectType { get; set; } = "";
        public string Subtype { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Criticality { get; set; } = "";
        public string Sensitivity { get; set; } = "";
        public string Environment { get; set; } = "";
        public Guid AreaId { get; set; }
        public string AreaName { get; set; } = "";
        public Guid? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public string LifecycleState { get; set; } = "";
        public string CustodyMode { get; set; } = "";
        public bool HasPayload { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool NoExpirationJustified { get; set; }
        public string? Thumbprint { get; set; }
        public int CurrentVersion { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? ModifiedAtUtc { get; set; }
        public Guid? ModifiedBy { get; set; }
        public byte[] RowVer { get; set; } = [];
        public string ExpirationStatus { get; set; } = "";
        public string? DetailsJson { get; set; }
        public int TotalCount { get; set; }
    }

    private sealed class DuplicateRow
    {
        public bool NameExists { get; set; }
        public bool ThumbprintExists { get; set; }
    }

    private sealed class WriteRow
    {
        public string? Code { get; set; }
        public byte[] RowVer { get; set; } = [];
        public int CurrentVersion { get; set; }
        public int RevokedAccesses { get; set; }
        public int CancelledRequests { get; set; }
    }

    private sealed class CertificateRow
    {
        public int VersionNumber { get; set; }
        public byte[] CertificateDer { get; set; } = [];
    }

    private sealed class VersionRow
    {
        public int VersionNumber { get; set; }
        public Guid ChangedBy { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAtUtc { get; set; }
        public string Reason { get; set; } = "";
        public string? ChangedFieldsJson { get; set; }
    }

    private sealed class OwnershipRow
    {
        public string OwnerRole { get; set; } = "";
        public Guid? PreviousUserId { get; set; }
        public string? PreviousUserName { get; set; }
        public Guid NewUserId { get; set; }
        public string? NewUserName { get; set; }
        public Guid ChangedBy { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAtUtc { get; set; }
        public string Reason { get; set; } = "";
    }
}
