using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Authorization;
using PlatformVault.Application.Users;

namespace PlatformVault.Application.Auditing;

public sealed record SearchAuditEventsQuery(AuditSearchCriteria Criteria, PageRequest Page);

public sealed record VerifyAuditChainCommand;

public sealed record GetDashboardQuery;

public sealed record ListJobRunsQuery(string? JobName);

/// <summary>US-040: consulta de la bitácora global (Auditor y Seguridad; el Administrador no, nota ¹ de §4.2).</summary>
public sealed class SearchAuditEventsHandler(ICurrentUser user, IAuditReader reader) : IQueryHandler<SearchAuditEventsQuery, PagedResult<AuditEventView>>
{
    public Task<PagedResult<AuditEventView>> HandleAsync(SearchAuditEventsQuery query, CancellationToken ct)
    {
        user.Require(Permission.VerifyAuditChain);
        if (query.Criteria.FromUtc is { } from && query.Criteria.ToUtc is { } to && from > to)
            throw new ValidationFailure("El rango de fechas no es válido.");
        return reader.SearchAsync(query.Criteria, query.Page, ct);
    }
}

/// <summary>RN-076: verificación bajo demanda de la integridad de la cadena.</summary>
public sealed class VerifyAuditChainHandler(ICurrentUser user, IClock clock, IAuditReader reader, AuditLogger audit)
    : ICommandHandler<VerifyAuditChainCommand, AuditIntegrityStatus>
{
    public async Task<AuditIntegrityStatus> HandleAsync(VerifyAuditChainCommand command, CancellationToken ct)
    {
        user.Require(Permission.VerifyAuditChain);
        var status = await reader.VerifyChainAsync(clock.UtcNow, ct);
        await audit.RecordAsync(status.IsIntact ? AuditActions.AuditChainVerified : AuditActions.AuditChainBroken, "AuditChain", null,
            status.IsIntact ? AuditResults.Success : AuditResults.Failed, new { status.EventsVerified, status.FirstBrokenSequence }, ct);
        return status;
    }
}

/// <summary>US-038: tablero operativo filtrado por el ámbito del usuario (RN-010).</summary>
public sealed class GetDashboardHandler(ICurrentUser user, IClock clock, IDashboardReader reader) : IQueryHandler<GetDashboardQuery, DashboardSummary>
{
    public Task<DashboardSummary> HandleAsync(GetDashboardQuery query, CancellationToken ct) => reader.GetAsync(user.Scope(), clock.UtcNow, ct);
}

/// <summary>Historial de trabajos en segundo plano (salud de la plataforma, solo Administrador).</summary>
public sealed class ListJobRunsHandler(ICurrentUser user, IJobRunRepository jobs) : IQueryHandler<ListJobRunsQuery, IReadOnlyList<JobRunView>>
{
    public Task<IReadOnlyList<JobRunView>> HandleAsync(ListJobRunsQuery query, CancellationToken ct)
    {
        user.Require(Permission.ViewJobs);
        return jobs.ListRecentAsync(query.JobName, 100, ct);
    }
}
