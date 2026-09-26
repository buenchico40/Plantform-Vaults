using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Access;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Expiration;
using PlatformVault.Application.Groups;
using PlatformVault.Application.Users;
using PlatformVault.Domain.Access;
using PlatformVault.Domain.Expiration;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Api.Controllers;

public sealed record CreateGroupRequest([Required, MaxLength(150)] string Name, [MaxLength(500)] string? Description,
    Guid? AreaId, [Required] Guid ResponsibleUserId);

public sealed record UpdateGroupRequest([Required, MaxLength(150)] string Name, [MaxLength(500)] string? Description,
    [Required] string Status);

public sealed record AddMemberRequest([Required] Guid UserId, bool IsResponsible);

/// <summary>Contrato CreateAccessRequest. En la iteración 1A cada solicitud cubre un objeto.</summary>
public sealed record CreateAccessRequestBody(
    [Required, MinLength(1), MaxLength(1)] IReadOnlyList<Guid> ObjectIds,
    [Required] AccessAction Action,
    [Required, MinLength(20), MaxLength(1000)] string Justification,
    DateTime? RequestedStartAt,
    [Range(1, 480)] int RequestedDurationMinutes);

public sealed record DecisionRequest([Required] Decision Decision, [MaxLength(1000)] string? Comment);

public sealed record RevokeRequest([Required, MaxLength(300)] string Reason);

public sealed record ExpirationPolicyRequest([Required, MaxLength(150)] string Name, ObjectType? AppliesToType,
    Criticality? AppliesToCriticality, [Required] IReadOnlyList<int> ThresholdDays, bool IsActive = true);

public sealed record AcknowledgeRequest([Required, MaxLength(500)] string Comment);

[Route("api/v1/groups")]
public sealed class GroupsController : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<GroupSummary>> Search([FromQuery] string? q, [FromQuery] string? status, [FromQuery] int? page,
        [FromQuery] int? pageSize, [FromServices] IQueryHandler<SearchGroupsQuery, PagedResult<GroupSummary>> handler, CancellationToken ct)
    {
        RejectUnsupported("isPersonal");
        bool? isActive = status switch
        {
            null or "" => null,
            "Activo" => true,
            "Inactivo" => false,
            _ => throw new ValidationFailure("status debe ser Activo o Inactivo."),
        };
        return handler.HandleAsync(new SearchGroupsQuery(q, isActive, Page(page, pageSize)), ct);
    }

    [HttpPost]
    public async Task<ActionResult<GroupCreated>> Create(CreateGroupRequest request,
        [FromServices] ICommandHandler<CreateGroupCommand, GroupCreated> handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CreateGroupCommand(request.Name, request.Description, request.AreaId, request.ResponsibleUserId), ct);
        return CreatedAtAction(nameof(Get), new { groupId = result.Id }, result);
    }

    [HttpGet("{groupId:guid}")]
    public Task<GroupDetail> Get(Guid groupId, [FromServices] IQueryHandler<GetGroupQuery, GroupDetail> handler, CancellationToken ct) =>
        handler.HandleAsync(new GetGroupQuery(groupId), ct);

    [HttpPatch("{groupId:guid}")]
    public Task<MembershipChangeResult> Update(Guid groupId, UpdateGroupRequest request,
        [FromServices] ICommandHandler<UpdateGroupCommand, MembershipChangeResult> handler, CancellationToken ct)
    {
        var active = request.Status switch
        {
            "Activo" => true,
            "Inactivo" => false,
            _ => throw new ValidationFailure("El estado debe ser Activo o Inactivo."),
        };
        return handler.HandleAsync(new UpdateGroupCommand(groupId, request.Name, request.Description, active), ct);
    }

    [HttpPost("{groupId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid groupId, AddMemberRequest request,
        [FromServices] ICommandHandler<AddGroupMemberCommand, Unit> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new AddGroupMemberCommand(groupId, request.UserId, request.IsResponsible), ct);
        return NoContent();
    }

    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    public Task<MembershipChangeResult> RemoveMember(Guid groupId, Guid userId,
        [FromServices] ICommandHandler<RemoveGroupMemberCommand, MembershipChangeResult> handler, CancellationToken ct) =>
        handler.HandleAsync(new RemoveGroupMemberCommand(groupId, userId), ct);
}

[Route("api/v1")]
public sealed class AccessController : ApiControllerBase
{
    [HttpGet("access-requests")]
    public Task<PagedResult<AccessRequestView>> List([FromQuery] AccessRequestScope scope, [FromQuery] RequestState? state, [FromQuery] int? page,
        [FromQuery] int? pageSize, [FromServices] IQueryHandler<ListAccessRequestsQuery, PagedResult<AccessRequestView>> handler, CancellationToken ct) =>
        handler.HandleAsync(new ListAccessRequestsQuery(scope, state, Page(page, pageSize)), ct);

    [HttpPost("access-requests")]
    public async Task<ActionResult<AccessRequestView>> Submit(CreateAccessRequestBody request,
        [FromServices] ICommandHandler<SubmitAccessRequestCommand, AccessRequestView> handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new SubmitAccessRequestCommand(request.ObjectIds[0], request.Action, request.Justification,
            request.RequestedStartAt, request.RequestedDurationMinutes), ct);
        return CreatedAtAction(nameof(Get), new { requestId = result.Id }, result);
    }

    [HttpGet("access-requests/{requestId:guid}")]
    public Task<AccessRequestView> Get(Guid requestId, [FromServices] IQueryHandler<GetAccessRequestQuery, AccessRequestView> handler,
        CancellationToken ct) => handler.HandleAsync(new GetAccessRequestQuery(requestId), ct);

    [HttpPost("access-requests/{requestId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid requestId, [FromServices] ICommandHandler<CancelAccessRequestCommand, Unit> handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(new CancelAccessRequestCommand(requestId), ct);
        return NoContent();
    }

    [HttpPost("access-requests/{requestId:guid}/approvals")]
    public Task<AccessRequestView> Decide(Guid requestId, DecisionRequest request,
        [FromServices] ICommandHandler<DecideAccessRequestCommand, AccessRequestView> handler, CancellationToken ct) =>
        handler.HandleAsync(new DecideAccessRequestCommand(requestId, request.Decision, request.Comment), ct);

    [HttpGet("temporary-access")]
    public Task<IReadOnlyList<TemporaryAccessView>> MyAccess([FromQuery] bool onlyCurrent,
        [FromServices] IQueryHandler<ListMyTemporaryAccessQuery, IReadOnlyList<TemporaryAccessView>> handler, CancellationToken ct) =>
        handler.HandleAsync(new ListMyTemporaryAccessQuery(onlyCurrent), ct);

    [HttpPost("temporary-access/{accessId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid accessId, RevokeRequest request,
        [FromServices] ICommandHandler<RevokeTemporaryAccessCommand, Unit> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new RevokeTemporaryAccessCommand(accessId, request.Reason), ct);
        return NoContent();
    }
}

[Route("api/v1")]
public sealed class ExpirationController : ApiControllerBase
{
    [HttpGet("expiration-policies")]
    public Task<IReadOnlyList<ExpirationPolicyView>> Policies(
        [FromServices] IQueryHandler<ListExpirationPoliciesQuery, IReadOnlyList<ExpirationPolicyView>> handler, CancellationToken ct) =>
        handler.HandleAsync(new ListExpirationPoliciesQuery(), ct);

    [HttpPost("expiration-policies")]
    public async Task<IActionResult> CreatePolicy(ExpirationPolicyRequest request,
        [FromServices] ICommandHandler<UpsertExpirationPolicyCommand, Guid> handler, CancellationToken ct)
    {
        var id = await handler.HandleAsync(new UpsertExpirationPolicyCommand(null, request.Name, request.AppliesToType, request.AppliesToCriticality,
            request.ThresholdDays, request.IsActive), ct);
        return Created($"/api/v1/expiration-policies/{id}", new { id });
    }

    [HttpPut("expiration-policies/{policyId:guid}")]
    public async Task<IActionResult> UpdatePolicy(Guid policyId, ExpirationPolicyRequest request,
        [FromServices] ICommandHandler<UpsertExpirationPolicyCommand, Guid> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new UpsertExpirationPolicyCommand(policyId, request.Name, request.AppliesToType, request.AppliesToCriticality,
            request.ThresholdDays, request.IsActive), ct);
        return NoContent();
    }

    [HttpGet("alerts")]
    public Task<PagedResult<AlertView>> Alerts([FromQuery] AlertState? state, [FromQuery] AlertSeverity? severity, [FromQuery] Guid? objectId,
        [FromQuery] bool? onlyOpen, [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromServices] IQueryHandler<SearchAlertsQuery, PagedResult<AlertView>> handler, CancellationToken ct) =>
        handler.HandleAsync(new SearchAlertsQuery(new AlertSearchCriteria(state, severity, onlyOpen ?? state is null, objectId), Page(page, pageSize)), ct);

    [HttpPost("alerts/{alertId:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid alertId, AcknowledgeRequest request,
        [FromServices] ICommandHandler<AcknowledgeAlertCommand, Unit> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new AcknowledgeAlertCommand(alertId, request.Comment), ct);
        return NoContent();
    }
}

[Route("api/v1")]
public sealed class AuditController : ApiControllerBase
{
    [HttpGet("audit-events")]
    public Task<PagedResult<AuditEventView>> Search([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? action,
        [FromQuery] string? actorId, [FromQuery] string? resourceType, [FromQuery] string? resourceId, [FromQuery] string? result,
        [FromQuery] Guid? correlationId, [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromServices] IQueryHandler<SearchAuditEventsQuery, PagedResult<AuditEventView>> handler, CancellationToken ct) =>
        handler.HandleAsync(new SearchAuditEventsQuery(new AuditSearchCriteria(from, to, action, actorId, resourceType, resourceId, result, correlationId),
            Page(page, pageSize)), ct);

    [HttpPost("audit-events/integrity")]
    public Task<AuditIntegrityStatus> Verify([FromServices] ICommandHandler<VerifyAuditChainCommand, AuditIntegrityStatus> handler,
        CancellationToken ct) => handler.HandleAsync(new VerifyAuditChainCommand(), ct);

    [HttpGet("dashboards/operational")]
    public Task<DashboardSummary> Dashboard([FromServices] IQueryHandler<GetDashboardQuery, DashboardSummary> handler, CancellationToken ct) =>
        handler.HandleAsync(new GetDashboardQuery(), ct);

    [HttpGet("jobs/runs")]
    public Task<IReadOnlyList<JobRunView>> JobRuns([FromQuery] string? jobName,
        [FromServices] IQueryHandler<ListJobRunsQuery, IReadOnlyList<JobRunView>> handler, CancellationToken ct) =>
        handler.HandleAsync(new ListJobRunsQuery(jobName), ct);
}
