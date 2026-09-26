using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Access;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Expiration;
using PlatformVault.Application.Users;
using PlatformVault.Domain.Expiration;
using PlatformVault.Domain.Objects;
using static PlatformVault.Infrastructure.Persistence.DbValues;

namespace PlatformVault.Infrastructure.Persistence;

public sealed class AlertRepository(StoredProcedures sp) : IAlertRepository
{
    public async Task<PagedResult<AlertView>> SearchAsync(AlertSearchCriteria criteria, PageRequest page, VisibilityScope scope, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<AlertRow>("app.usp_Alert_Search", new
        {
            scope.ViewerUserId,
            scope.HasGlobalScope,
            scope.CustodianAreaId,
            State = criteria.State?.ToString(),
            Severity = criteria.Severity?.ToString(),
            criteria.OnlyOpen,
            criteria.ObjectId,
            page.Offset,
            PageSize = page.SafePageSize,
        }, ct);
        var items = rows.Select(r => new AlertView(r.AlertId, r.ObjectId, r.ObjectCode, r.ObjectName, Enum<ObjectType>(r.ObjectType),
            Utc(r.ExpirationDate), r.ThresholdDays, Enum<AlertKind>(r.Kind), Enum<AlertSeverity>(r.Severity), Enum<AlertState>(r.State),
            r.EscalationLevel, Utc(r.CreatedAtUtc), Utc(r.LastEscalatedAtUtc), r.AcknowledgedBy, Utc(r.AcknowledgedAtUtc), r.AcknowledgeComment,
            Utc(r.ResolvedAtUtc), r.ResolutionReason)).ToList();
        return new PagedResult<AlertView>(items, page.SafePage, page.SafePageSize, rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    public async Task<AlertRecord?> GetAsync(Guid alertId, CancellationToken ct)
    {
        var r = await sp.QuerySingleOrDefaultAsync<AlertRow>("app.usp_Alert_GetById", new { AlertId = alertId }, ct);
        return r is null ? null : ToRecord(r);
    }

    public async Task<bool> InsertIfNotExistsAsync(Guid alertId, Guid objectId, AlertCandidate candidate, DateTime nowUtc, CancellationToken ct) =>
        await sp.QuerySingleOrDefaultAsync<bool>("app.usp_Alert_InsertIfNotExists", new
        {
            AlertId = alertId,
            ObjectId = objectId,
            candidate.AlertKey,
            Kind = candidate.Kind.ToString(),
            candidate.ThresholdDays,
            Severity = candidate.Severity.ToString(),
            NowUtc = nowUtc,
            candidate.InitialLevel,
        }, ct);

    public Task AcknowledgeAsync(Guid alertId, Guid userId, string comment, DateTime nowUtc, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_Alert_Acknowledge", new { AlertId = alertId, UserId = userId, Comment = comment, NowUtc = nowUtc }, ct);

    public async Task<bool> EscalateAsync(Guid alertId, byte newLevel, DateTime nowUtc, CancellationToken ct) =>
        await sp.QuerySingleOrDefaultAsync<int>("app.usp_Alert_Escalate", new { AlertId = alertId, NewLevel = newLevel, NowUtc = nowUtc }, ct) > 0;

    public async Task<IReadOnlyList<AlertRecord>> GetEscalationCandidatesAsync(DateTime nowUtc, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<AlertRow>("app.usp_Alert_GetEscalationCandidates", new
        {
            NowUtc = nowUtc,
            CriticalHours = (int)EscalationPolicy.WindowFor(AlertSeverity.Critical).TotalHours,
            HighHours = (int)EscalationPolicy.WindowFor(AlertSeverity.High).TotalHours,
            MediumHours = (int)EscalationPolicy.WindowFor(AlertSeverity.Medium).TotalHours,
            LowHours = (int)EscalationPolicy.WindowFor(AlertSeverity.Low).TotalHours,
        }, ct);
        return rows.Select(ToRecord).ToList();
    }

    public Task<IReadOnlyList<UserContact>> GetEscalationRecipientsAsync(Guid objectId, byte level, CancellationToken ct) =>
        sp.ContactsAsync("app.usp_Escalation_GetRecipients", new { ObjectId = objectId, Level = level }, ct);

    private static AlertRecord ToRecord(AlertRow r) => new(r.AlertId, r.ObjectId, r.AlertKey, Enum<AlertSeverity>(r.Severity),
        string.IsNullOrEmpty(r.State) ? AlertState.Open : Enum<AlertState>(r.State), r.EscalationLevel);

    private sealed class AlertRow
    {
        public Guid AlertId { get; set; }
        public Guid ObjectId { get; set; }
        public string ObjectCode { get; set; } = "";
        public string ObjectName { get; set; } = "";
        public string ObjectType { get; set; } = "";
        public DateTime? ExpirationDate { get; set; }
        public string AlertKey { get; set; } = "";
        public string Kind { get; set; } = "";
        public int? ThresholdDays { get; set; }
        public string Severity { get; set; } = "";
        public string State { get; set; } = "";
        public byte EscalationLevel { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? LastEscalatedAtUtc { get; set; }
        public Guid? AcknowledgedBy { get; set; }
        public DateTime? AcknowledgedAtUtc { get; set; }
        public string? AcknowledgeComment { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
        public string? ResolutionReason { get; set; }
        public int TotalCount { get; set; }
    }
}

public sealed class ExpirationRepository(StoredProcedures sp) : IExpirationRepository
{
    public async Task<IReadOnlyList<ExpirationPolicyView>> GetPoliciesAsync(CancellationToken ct)
    {
        var rows = await sp.QueryAsync<PolicyRow>("app.usp_ExpirationPolicy_GetAll", null, ct);
        return rows.Select(r => new ExpirationPolicyView(r.PolicyId, r.Name, OptionalEnum<ObjectType>(r.ObjectType), OptionalEnum<Criticality>(r.Criticality),
            Split(r.ThresholdDays).Select(d => int.Parse(d, CultureInfo.InvariantCulture)).ToList(), r.IsActive, Utc(r.ModifiedAtUtc))).ToList();
    }

    public Task UpsertPolicyAsync(Guid policyId, string name, ObjectType? type, Criticality? criticality, string thresholds, bool isActive,
        Guid actorId, DateTime nowUtc, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_ExpirationPolicy_Upsert", new
        {
            PolicyId = policyId,
            Name = name,
            ObjectType = type?.ToString(),
            Criticality = criticality?.ToString(),
            ThresholdDays = thresholds,
            IsActive = isActive,
            ModifiedBy = actorId,
            NowUtc = nowUtc,
        }, ct);

    public async Task<IReadOnlyList<MonitoringCandidate>> GetMonitoringCandidatesAsync(DateTime nowUtc, int horizonDays, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<CandidateRow>("app.usp_Expiration_GetMonitoringCandidates", new { NowUtc = nowUtc, HorizonDays = horizonDays }, ct);
        return rows.Select(r => new MonitoringCandidate(r.ObjectId, r.Code, r.Name, Enum<ObjectType>(r.ObjectType), Enum<Criticality>(r.Criticality),
            Utc(r.ExpirationDate), r.FunctionalOwnerId, r.TechnicalOwnerId)).ToList();
    }

    private sealed class PolicyRow
    {
        public Guid PolicyId { get; set; }
        public string Name { get; set; } = "";
        public string? ObjectType { get; set; }
        public string? Criticality { get; set; }
        public string ThresholdDays { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime ModifiedAtUtc { get; set; }
    }

    private sealed class CandidateRow
    {
        public Guid ObjectId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string ObjectType { get; set; } = "";
        public string Criticality { get; set; } = "";
        public DateTime ExpirationDate { get; set; }
        public Guid? FunctionalOwnerId { get; set; }
        public Guid? TechnicalOwnerId { get; set; }
    }
}

public sealed class AuditRepository(StoredProcedures sp) : IAuditTrail, IAuditReader
{
    public Task AppendAsync(AuditEntry entry, CancellationToken ct) =>
        sp.ExecuteAsync("audit.usp_AuditEvent_Append", new
        {
            entry.EventId,
            entry.TimestampUtc,
            entry.ActorType,
            entry.ActorId,
            entry.ActorName,
            entry.Action,
            entry.ResourceType,
            entry.ResourceId,
            entry.Result,
            entry.SourceIp,
            entry.CorrelationId,
            entry.Details,
        }, ct);

    public Task AppendUsageAsync(UsageEntry entry, CancellationToken ct) =>
        sp.ExecuteAsync("audit.usp_UsageEvent_Append", new
        {
            entry.ObjectId,
            entry.ActorType,
            entry.ActorId,
            entry.Action,
            entry.TimestampUtc,
            entry.Channel,
            entry.Result,
            entry.SourceIp,
            entry.CorrelationId,
        }, ct);

    public async Task<PagedResult<AuditEventView>> SearchAsync(AuditSearchCriteria criteria, PageRequest page, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<EventRow>("audit.usp_AuditEvent_Search", new
        {
            criteria.FromUtc,
            criteria.ToUtc,
            criteria.ActorId,
            criteria.Action,
            criteria.ResourceType,
            criteria.ResourceId,
            criteria.Result,
            criteria.CorrelationId,
            PageNumber = page.SafePage,
            PageSize = page.SafePageSize,
        }, ct);
        return new PagedResult<AuditEventView>(rows.Select(ToView).ToList(), page.SafePage, page.SafePageSize, rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    public async Task<IReadOnlyList<AuditEventView>> ListByResourceAsync(string resourceType, string resourceId, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<EventRow>("audit.usp_AuditEvent_ListByResource",
            new { ResourceType = resourceType, ResourceId = resourceId, Top = 200 }, ct);
        return rows.Select(ToView).ToList();
    }

    public async Task<IReadOnlyList<UsageEventView>> ListUsageByObjectAsync(Guid objectId, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<UsageRow>("audit.usp_UsageEvent_ListByObject", new { ObjectId = objectId, Top = 200 }, ct);
        return rows.Select(r => new UsageEventView(Utc(r.TimestampUtc), r.ActorType, r.ActorName ?? r.ActorId, r.Action, r.Channel, r.Result, r.SourceIp))
            .ToList();
    }

    public async Task<AuditIntegrityStatus> VerifyChainAsync(DateTime nowUtc, CancellationToken ct)
    {
        var row = await sp.QuerySingleOrDefaultAsync<ChainRow>("audit.usp_AuditChain_Verify", new { FromSequence = (long?)null, ToSequence = (long?)null }, ct);
        return new AuditIntegrityStatus(row?.EventsVerified ?? 0, row?.FirstBrokenSequence, row?.FromSequence, row?.ToSequence, nowUtc);
    }

    private static AuditEventView ToView(EventRow r) => new(r.SequenceNumber, r.EventId, Utc(r.TimestampUtc), r.ActorType, r.ActorId, r.ActorName,
        r.Action, r.ResourceType, r.ResourceId, r.Result, r.SourceIp, r.CorrelationId, r.Details);

    private sealed class EventRow
    {
        public long SequenceNumber { get; set; }
        public Guid EventId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string ActorType { get; set; } = "";
        public string? ActorId { get; set; }
        public string? ActorName { get; set; }
        public string Action { get; set; } = "";
        public string? ResourceType { get; set; }
        public string? ResourceId { get; set; }
        public string Result { get; set; } = "";
        public string? SourceIp { get; set; }
        public Guid? CorrelationId { get; set; }
        public string? Details { get; set; }
        public int TotalCount { get; set; }
    }

    private sealed class UsageRow
    {
        public DateTime TimestampUtc { get; set; }
        public string ActorType { get; set; } = "";
        public string ActorId { get; set; } = "";
        public string? ActorName { get; set; }
        public string Action { get; set; } = "";
        public string Channel { get; set; } = "";
        public string Result { get; set; } = "";
        public string? SourceIp { get; set; }
    }

    private sealed class ChainRow
    {
        public long EventsVerified { get; set; }
        public long? FirstBrokenSequence { get; set; }
        public long? FromSequence { get; set; }
        public long? ToSequence { get; set; }
    }
}

public sealed class AreaRepository(StoredProcedures sp) : IAreaRepository
{
    public async Task<IReadOnlyList<AreaView>> GetAllAsync(CancellationToken ct)
    {
        var rows = await sp.QueryAsync<AreaRow>("app.usp_Area_GetAll", null, ct);
        return rows.Select(r => new AreaView(r.AreaId, r.Code, r.Name, r.IsActive)).ToList();
    }

    public Task InsertAsync(Guid areaId, string code, string name, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_Area_Insert", new { AreaId = areaId, Code = code, Name = name }, ct);

    private sealed class AreaRow
    {
        public Guid AreaId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }
}

public sealed class DashboardReader(StoredProcedures sp) : IDashboardReader
{
    public Task<DashboardSummary> GetAsync(VisibilityScope scope, DateTime nowUtc, CancellationToken ct) =>
        sp.QueryMultipleAsync("report.usp_Dashboard_Get", new
        {
            scope.ViewerUserId,
            scope.HasGlobalScope,
            scope.CustodianAreaId,
            NowUtc = nowUtc,
            ExpiringSoonDays = ManagedObject.ExpiringSoonDaysDefault,
        }, async grid =>
        {
            var s = await grid.ReadFirstAsync<SummaryRow>();
            async Task<IReadOnlyList<CountByKey>> Next() => (await grid.ReadAsync<CountRow>()).Select(c => new CountByKey(c.Key, c.Count)).ToList();
            var byType = await Next();
            var byCriticality = await Next();
            var bySensitivity = await Next();
            var byState = await Next();
            var byExpiration = await Next();
            return new DashboardSummary(s.Total, s.Active ?? 0, s.Expired ?? 0, s.ExpiringSoon ?? 0, s.WithoutOwner ?? 0, s.OpenAlerts,
                s.PendingRequests, byType, byCriticality, bySensitivity, byState, byExpiration);
        }, ct);

    private sealed class SummaryRow
    {
        public int Total { get; set; }
        public int? Active { get; set; }
        public int? Expired { get; set; }
        public int? ExpiringSoon { get; set; }
        public int? WithoutOwner { get; set; }
        public int OpenAlerts { get; set; }
        public int PendingRequests { get; set; }
    }

    private sealed class CountRow
    {
        public string Key { get; set; } = "";
        public int Count { get; set; }
    }
}

public sealed class JobRunRepository(StoredProcedures sp) : IJobRunRepository
{
    public async Task<long> StartAsync(string jobName, DateTime nowUtc, CancellationToken ct) =>
        await sp.QuerySingleOrDefaultAsync<long>("app.usp_JobRun_Start", new { JobName = jobName, NowUtc = nowUtc }, ct);

    public Task FinishAsync(long runId, string status, int itemsProcessed, string? detail, DateTime nowUtc, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_JobRun_Finish",
            new { RunId = runId, Status = status, ItemsProcessed = itemsProcessed, Detail = detail, NowUtc = nowUtc }, ct);

    public async Task<IReadOnlyList<JobRunView>> ListRecentAsync(string? jobName, int top, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<RunRow>("app.usp_JobRun_ListRecent", new { JobName = jobName, Top = top }, ct);
        return rows.Select(r => new JobRunView(r.RunId, r.JobName, Utc(r.StartedAtUtc), Utc(r.FinishedAtUtc), r.Status, r.ItemsProcessed, r.Detail))
            .ToList();
    }

    private sealed class RunRow
    {
        public long RunId { get; set; }
        public string JobName { get; set; } = "";
        public DateTime StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public string Status { get; set; } = "";
        public int ItemsProcessed { get; set; }
        public string? Detail { get; set; }
    }
}

/// <summary>
/// Bloqueo de trabajo con sp_getapplock de sesión: usa una conexión propia que se mantiene abierta mientras dura el trabajo,
/// de modo que una sola instancia de la API ejecuta cada trabajo (IMP-13).
/// </summary>
public sealed class SqlJobLock(IOptions<DatabaseOptions> options) : IJobLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(string jobName, CancellationToken ct)
    {
        var connection = new SqlConnection(options.Value.ConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand("app.usp_Job_AcquireLock", connection) { CommandType = System.Data.CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@JobName", jobName);
            var acquired = (bool)(await command.ExecuteScalarAsync(ct) ?? false);
            if (acquired)
                return new Lease(connection, jobName);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
        await connection.DisposeAsync();
        return null;
    }

    private sealed class Lease(SqlConnection connection, string jobName) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new SqlCommand("app.usp_Job_ReleaseLock", connection) { CommandType = System.Data.CommandType.StoredProcedure };
                command.Parameters.AddWithValue("@JobName", jobName);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}

public sealed class NotificationRepository(StoredProcedures sp) : INotificationQueue, INotificationOutbox
{
    public const string EmailChannel = "Email";

    public async Task EnqueueAsync(IEnumerable<UserContact> recipients, string subject, string body, CancellationToken ct)
    {
        foreach (var recipient in recipients.Where(r => !string.IsNullOrWhiteSpace(r.Email)).DistinctBy(r => r.Email, StringComparer.OrdinalIgnoreCase))
        {
            await sp.ExecuteAsync("app.usp_Notification_Enqueue",
                new { Channel = EmailChannel, Recipient = recipient.Email, Subject = subject.Length > 300 ? subject[..300] : subject, Body = body }, ct);
        }
    }

    public async Task<IReadOnlyList<PendingNotification>> GetPendingAsync(int top, int maxAttempts, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<PendingRow>("app.usp_Notification_GetPending", new { Top = top, MaxAttempts = maxAttempts }, ct);
        return rows.Select(r => new PendingNotification(r.NotificationId, r.Channel, r.Recipient, r.Subject, r.Body, r.Attempts)).ToList();
    }

    public Task MarkResultAsync(long notificationId, string status, string? error, DateTime nowUtc, CancellationToken ct) =>
        sp.ExecuteAsync("app.usp_Notification_MarkResult",
            new { NotificationId = notificationId, Status = status, Error = error, NowUtc = nowUtc }, ct);

    private sealed class PendingRow
    {
        public long NotificationId { get; set; }
        public string Channel { get; set; } = "";
        public string Recipient { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public int Attempts { get; set; }
    }
}
