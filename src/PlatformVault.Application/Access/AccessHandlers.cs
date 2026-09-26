using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;
using PlatformVault.Application.Notifications;
using PlatformVault.Domain.Access;
using PlatformVault.Domain.Identity;

namespace PlatformVault.Application.Access;

public sealed record SubmitAccessRequestCommand(Guid ObjectId, AccessAction Action, string Justification, DateTime? RequestedStartAt,
    int RequestedDurationMinutes);

public sealed record DecideAccessRequestCommand(Guid RequestId, Decision Decision, string? Comment);

public sealed record CancelAccessRequestCommand(Guid RequestId);

public sealed record GetAccessRequestQuery(Guid RequestId);

public sealed record ListAccessRequestsQuery(AccessRequestScope Scope, RequestState? State, PageRequest Page);

public sealed record ListMyTemporaryAccessQuery(bool OnlyCurrent);

public sealed record RevokeTemporaryAccessCommand(Guid AccessId, string Reason);

/// <summary>US-031: solicitud de acceso temporal (RN-047 a RN-050).</summary>
public sealed class SubmitAccessRequestHandler(ICurrentUser user, IClock clock, ObjectAuthorizer authorizer, IObjectRepository objects,
    IAccessRequestRepository requests, IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier)
    : ICommandHandler<SubmitAccessRequestCommand, AccessRequestView>
{
    public async Task<AccessRequestView> HandleAsync(SubmitAccessRequestCommand command, CancellationToken ct)
    {
        await authorizer.AuthorizeAsync(command.ObjectId, ObjectOperation.RequestAccess, ct);
        var obj = await objects.LoadAsync(command.ObjectId, ct) ?? throw new NotFoundFailure("objeto");
        var now = clock.UtcNow;
        var validated = AccessRequestRules.ValidateNew(obj, command.Action, command.Justification, command.RequestedStartAt,
            command.RequestedDurationMinutes, now);

        if (await requests.ExistsPendingAsync(user.UserId, obj.ObjectId, validated.Action, ct))
            throw new ConflictFailure("DUPLICATE_REQUEST", "Ya tiene una solicitud pendiente para este objeto y acción.");
        var approvers = await requests.GetEligibleApproversAsync(obj.ObjectId, user.UserId, validated.ApproverKind, ct);
        if (approvers.Count == 0)
            throw new ConflictFailure("NO_ELIGIBLE_APPROVER", "No hay aprobadores elegibles para esta solicitud (RN-106, RN-122).");

        var requestId = Guid.NewGuid();
        await using var tx = await unitOfWork.BeginAsync(ct);
        var code = await requests.InsertAsync(new NewAccessRequest(requestId, user.UserId, obj.ObjectId, validated, now), ct);
        await audit.SuccessAsync(AuditActions.AccessRequested, "AccessRequest", requestId.ToString(), new
        {
            code,
            objectId = obj.ObjectId,
            objectCode = obj.Code,
            action = validated.Action.ToString(),
            validated.DurationMinutes,
            approverKind = validated.ApproverKind.ToString(),
        }, ct);
        await notifier.NotifyAsync(approvers, $"Solicitud {code} pendiente de aprobación",
            $"{user.DisplayName} solicita {validated.Action} sobre {obj.Code} durante {validated.DurationMinutes} minutos. Revise la solicitud en PlatformVault.", ct);
        await notifier.NotifyGroupActivityAsync(obj.ObjectId, obj.Code, "solicitar acceso", ct);
        await tx.CommitAsync(ct);

        return await requests.GetAsync(requestId, ct) ?? throw new NotFoundFailure("solicitud");
    }
}

/// <summary>US-033: aprobación o rechazo por un aprobador elegible distinto del solicitante (RN-051, RN-054, RN-106, RN-122).</summary>
public sealed class DecideAccessRequestHandler(ICurrentUser user, IClock clock, IAccessRequestRepository requests, IUserDirectory users,
    IUnitOfWork unitOfWork, AuditLogger audit, Notifier notifier) : ICommandHandler<DecideAccessRequestCommand, AccessRequestView>
{
    public async Task<AccessRequestView> HandleAsync(DecideAccessRequestCommand command, CancellationToken ct)
    {
        var request = await requests.GetAsync(command.RequestId, ct) ?? throw new NotFoundFailure("solicitud");
        var approvers = await requests.GetEligibleApproversAsync(request.ObjectId, request.RequesterId, request.ApproverKind, ct);
        if (approvers.All(a => a.UserId != user.UserId))
        {
            await audit.DeniedAsync(AuditActions.AccessDecided, "AccessRequest", request.Id.ToString(), new { reason = "NotEligibleApprover" }, ct);
            if (request.RequesterId == user.UserId)
                throw new ForbiddenFailure("Nadie puede aprobar su propia solicitud (RN-054).");
            throw new NotFoundFailure("solicitud");
        }
        if (request.ApproverKind == ApproverKind.Security && !user.Has(Permission.ApproveCriticalAccess))
            throw new ForbiddenFailure("Los accesos a objetos Críticos los aprueba Seguridad (RN-122).");

        var now = clock.UtcNow;
        user.EnsureRecentlyReauthenticated(now);
        if (request.State != RequestState.Pending)
            throw new ConflictFailure("REQUEST_NOT_PENDING", "La solicitud ya no está pendiente.");
        var comment = AccessRequestRules.ValidateDecision(user.UserId, request.RequesterId, command.Decision, command.Comment);

        Guid? accessId = null;
        DateTime? start = null, end = null;
        if (command.Decision == Decision.Approved)
        {
            accessId = Guid.NewGuid();
            (start, end) = AccessRequestRules.AccessWindow(request.RequestedStartAt, request.RequestedDurationMinutes, now);
        }

        await using var tx = await unitOfWork.BeginAsync(ct);
        await requests.DecideAsync(request.Id, user.UserId, command.Decision, comment, now, accessId, start, end, ct);
        await audit.SuccessAsync(AuditActions.AccessDecided, "AccessRequest", request.Id.ToString(), new
        {
            code = request.Code,
            decision = command.Decision.ToString(),
            objectId = request.ObjectId,
            temporaryAccessId = accessId,
            startUtc = start,
            endUtc = end,
        }, ct);
        var requester = await users.GetContactsAsync([request.RequesterId], ct);
        var text = command.Decision == Decision.Approved
            ? $"Su solicitud {request.Code} sobre {request.ObjectCode} fue aprobada. Acceso vigente de {start:yyyy-MM-dd HH:mm} a {end:yyyy-MM-dd HH:mm} (UTC)."
            : $"Su solicitud {request.Code} sobre {request.ObjectCode} fue rechazada. Motivo: {comment}";
        await notifier.NotifyAsync(requester, $"Solicitud {request.Code} {(command.Decision == Decision.Approved ? "aprobada" : "rechazada")}", text, ct);
        await tx.CommitAsync(ct);
        return await requests.GetAsync(request.Id, ct) ?? throw new NotFoundFailure("solicitud");
    }
}

public sealed class CancelAccessRequestHandler(ICurrentUser user, IClock clock, IAccessRequestRepository requests, IUnitOfWork unitOfWork,
    AuditLogger audit) : ICommandHandler<CancelAccessRequestCommand, Unit>
{
    public async Task<Unit> HandleAsync(CancelAccessRequestCommand command, CancellationToken ct)
    {
        var request = await requests.GetAsync(command.RequestId, ct);
        if (request is null || request.RequesterId != user.UserId)
            throw new NotFoundFailure("solicitud");
        await using var tx = await unitOfWork.BeginAsync(ct);
        await requests.CancelAsync(command.RequestId, user.UserId, clock.UtcNow, ct);
        await audit.SuccessAsync(AuditActions.AccessRequestCancelled, "AccessRequest", command.RequestId.ToString(), new { code = request.Code }, ct);
        await tx.CommitAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Detalle visible para el solicitante, sus aprobadores elegibles, Auditor y Seguridad.</summary>
public sealed class GetAccessRequestHandler(ICurrentUser user, IAccessRequestRepository requests)
    : IQueryHandler<GetAccessRequestQuery, AccessRequestView>
{
    public async Task<AccessRequestView> HandleAsync(GetAccessRequestQuery query, CancellationToken ct)
    {
        var request = await requests.GetAsync(query.RequestId, ct) ?? throw new NotFoundFailure("solicitud");
        if (request.RequesterId == user.UserId || user.IsInRole(SystemRoles.Auditor) || user.IsInRole(SystemRoles.Security))
            return request;
        var approvers = await requests.GetEligibleApproversAsync(request.ObjectId, request.RequesterId, request.ApproverKind, ct);
        return approvers.Any(a => a.UserId == user.UserId) ? request : throw new NotFoundFailure("solicitud");
    }
}

public sealed class ListAccessRequestsHandler(ICurrentUser user, IAccessRequestRepository requests)
    : IQueryHandler<ListAccessRequestsQuery, PagedResult<AccessRequestView>>
{
    public Task<PagedResult<AccessRequestView>> HandleAsync(ListAccessRequestsQuery query, CancellationToken ct)
    {
        if (query.Scope == AccessRequestScope.All && !user.IsInRole(SystemRoles.Auditor) && !user.IsInRole(SystemRoles.Security))
            throw new ForbiddenFailure();
        return requests.SearchAsync(query.Scope, user.UserId, user.IsInRole(SystemRoles.Security), query.State, query.Page, ct);
    }
}

public sealed class ListMyTemporaryAccessHandler(ICurrentUser user, ITemporaryAccessRepository accesses)
    : IQueryHandler<ListMyTemporaryAccessQuery, IReadOnlyList<TemporaryAccessView>>
{
    public Task<IReadOnlyList<TemporaryAccessView>> HandleAsync(ListMyTemporaryAccessQuery query, CancellationToken ct) =>
        accesses.ListByUserAsync(user.UserId, query.OnlyCurrent, ct);
}

/// <summary>RN-058: revocan el beneficiario, un propietario del objeto o Seguridad.</summary>
public sealed class RevokeTemporaryAccessHandler(ICurrentUser user, IClock clock, ITemporaryAccessRepository accesses,
    IObjectRepository objects, IUnitOfWork unitOfWork, AuditLogger audit) : ICommandHandler<RevokeTemporaryAccessCommand, Unit>
{
    public async Task<Unit> HandleAsync(RevokeTemporaryAccessCommand command, CancellationToken ct)
    {
        var access = await accesses.GetAsync(command.AccessId, ct) ?? throw new NotFoundFailure("acceso temporal");
        var context = await objects.GetAuthorizationContextAsync(access.ObjectId, user.UserId, clock.UtcNow, ct);
        var allowed = access.BeneficiaryId == user.UserId || user.IsInRole(SystemRoles.Security) || context?.IsOwner(user.UserId) == true;
        if (!allowed)
            throw new NotFoundFailure("acceso temporal");
        var reason = command.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 300)
            throw new ValidationFailure("El motivo es obligatorio (máximo 300 caracteres).");

        await using var tx = await unitOfWork.BeginAsync(ct);
        await accesses.RevokeAsync(access.AccessId, user.UserId, reason, clock.UtcNow, ct);
        await audit.SuccessAsync(AuditActions.TemporaryAccessRevoked, "TemporaryAccess", access.AccessId.ToString(),
            new { objectId = access.ObjectId, beneficiaryId = access.BeneficiaryId, reason }, ct);
        await tx.CommitAsync(ct);
        return Unit.Value;
    }
}
