using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Expiration;
using PlatformVault.Domain.Identity;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Expiration;

public sealed record ListExpirationPoliciesQuery;

public sealed record UpsertExpirationPolicyCommand(Guid? PolicyId, string Name, ObjectType? AppliesToType, Criticality? AppliesToCriticality,
    IReadOnlyList<int> ThresholdDays, bool IsActive);

public sealed record SearchAlertsQuery(AlertSearchCriteria Criteria, PageRequest Page);

public sealed record AcknowledgeAlertCommand(Guid AlertId, string Comment);

public sealed class ListExpirationPoliciesHandler(ICurrentUser user, IExpirationRepository repository)
    : IQueryHandler<ListExpirationPoliciesQuery, IReadOnlyList<ExpirationPolicyView>>
{
    public Task<IReadOnlyList<ExpirationPolicyView>> HandleAsync(ListExpirationPoliciesQuery query, CancellationToken ct)
    {
        user.Require(Permission.ViewAlerts);
        return repository.GetPoliciesAsync(ct);
    }
}

/// <summary>
/// US-020: Seguridad define umbrales por tipo y criticidad (RN-061, RN-062).
/// Los cuatro ojos de RN-046 sobre políticas pertenecen a la iteración 1B.
/// </summary>
public sealed class UpsertExpirationPolicyHandler(ICurrentUser user, IClock clock, IExpirationRepository repository, IUnitOfWork unitOfWork,
    AuditLogger audit) : ICommandHandler<UpsertExpirationPolicyCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertExpirationPolicyCommand command, CancellationToken ct)
    {
        user.Require(Permission.ManageExpirationPolicies);
        var name = command.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 150)
            throw new ValidationFailure("El nombre de la política es obligatorio (máximo 150 caracteres).");
        var schedule = ThresholdSchedule.Parse(string.Join(',', command.ThresholdDays)).ValidateFor(command.AppliesToCriticality);
        if (schedule.ToString().Length > 100)
            throw new DomainException(DomainErrors.InvalidThresholds, "Demasiados umbrales.");

        var policyId = command.PolicyId ?? Guid.NewGuid();
        await using var tx = await unitOfWork.BeginAsync(ct);
        await repository.UpsertPolicyAsync(policyId, name, command.AppliesToType, command.AppliesToCriticality, schedule.ToString(),
            command.IsActive, user.UserId, clock.UtcNow, ct);
        await audit.SuccessAsync(AuditActions.PolicyChanged, "ExpirationPolicy", policyId.ToString(), new
        {
            name,
            type = command.AppliesToType?.ToString(),
            criticality = command.AppliesToCriticality?.ToString(),
            thresholds = schedule.ToString(),
            command.IsActive,
        }, ct);
        await tx.CommitAsync(ct);
        return policyId;
    }
}

public sealed class SearchAlertsHandler(ICurrentUser user, IAlertRepository alerts) : IQueryHandler<SearchAlertsQuery, PagedResult<AlertView>>
{
    public Task<PagedResult<AlertView>> HandleAsync(SearchAlertsQuery query, CancellationToken ct) =>
        alerts.SearchAsync(query.Criteria, query.Page, user.Scope(), ct);
}

/// <summary>US-024: reconocer una alerta detiene su escalamiento pero no la resuelve (RN-070).</summary>
public sealed class AcknowledgeAlertHandler(ICurrentUser user, IClock clock, IAlertRepository alerts, ObjectAuthorizer authorizer,
    IUnitOfWork unitOfWork, AuditLogger audit) : ICommandHandler<AcknowledgeAlertCommand, Unit>
{
    public async Task<Unit> HandleAsync(AcknowledgeAlertCommand command, CancellationToken ct)
    {
        var comment = command.Comment?.Trim();
        if (string.IsNullOrEmpty(comment) || comment.Length > 500)
            throw new ValidationFailure("El comentario es obligatorio (máximo 500 caracteres).");
        var alert = await alerts.GetAsync(command.AlertId, ct) ?? throw new NotFoundFailure("alerta");
        var context = await authorizer.AuthorizeAsync(alert.ObjectId, ObjectOperation.View, ct);
        var responsible = context.IsOwner(user.UserId) || user.IsInRole(SystemRoles.Security)
            || (user.IsInRole(SystemRoles.Custodian) && ObjectAuthorizer.HasScopedAccess(context, user));
        if (!responsible)
            throw new ForbiddenFailure("Reconocen la alerta los propietarios, los custodios del objeto o Seguridad.");

        await using var tx = await unitOfWork.BeginAsync(ct);
        await alerts.AcknowledgeAsync(alert.AlertId, user.UserId, comment, clock.UtcNow, ct);
        await audit.SuccessAsync(AuditActions.AlertAcknowledged, "Alert", alert.AlertId.ToString(),
            new { objectId = alert.ObjectId, alert.AlertKey, comment }, ct);
        await tx.CommitAsync(ct);
        return Unit.Value;
    }
}
