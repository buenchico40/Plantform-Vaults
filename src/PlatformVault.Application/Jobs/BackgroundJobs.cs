using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Expiration;
using PlatformVault.Application.Notifications;
using PlatformVault.Domain.Expiration;
using PlatformVault.Domain.Identity;

namespace PlatformVault.Application.Jobs;

/// <summary>Trabajo en segundo plano. Devuelve el número de elementos procesados.</summary>
public interface IBackgroundJob
{
    string Name { get; }
    TimeSpan Interval { get; }
    Task<int> RunAsync(CancellationToken ct);
}

/// <summary>US-021: monitoreo de vencimientos con alertas idempotentes (RN-061 a RN-068, RN-071).</summary>
public sealed class ExpirationMonitorJob(IClock clock, IExpirationRepository expiration, IAlertRepository alerts, Notifier notifier,
    AuditLogger audit) : IBackgroundJob
{
    public string Name => "ExpirationMonitor";
    public TimeSpan Interval => TimeSpan.FromHours(1);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var policies = (await expiration.GetPoliciesAsync(ct))
            .Select(p => new ExpirationPolicyDefinition(p.Id, p.Name, p.AppliesToType, p.AppliesToCriticality,
                ThresholdSchedule.Parse(string.Join(',', p.ThresholdDays)), p.IsActive))
            .ToList();
        var fallback = ThresholdSchedule.Parse(string.Join(',', ThresholdSchedule.Default));
        var horizon = policies.SelectMany(p => p.Thresholds.Days).Append(fallback.Days[0]).Max();

        var raised = 0;
        foreach (var candidate in await expiration.GetMonitoringCandidatesAsync(now, horizon, ct))
        {
            var schedule = ExpirationEvaluator.SelectPolicy(policies, candidate.ObjectType, candidate.Criticality)?.Thresholds ?? fallback;
            var alert = ExpirationEvaluator.Evaluate(candidate.ExpirationDate, candidate.Criticality, schedule, now);
            if (alert is null)
                continue;
            var alertId = Guid.NewGuid();
            if (!await alerts.InsertIfNotExistsAsync(alertId, candidate.ObjectId, alert, now, ct))
                continue;

            raised++;
            await audit.SuccessAsync(AuditActions.AlertRaised, "Alert", alertId.ToString(),
                new { objectId = candidate.ObjectId, alert.AlertKey, severity = alert.Severity.ToString(), level = alert.InitialLevel }, ct);
            for (byte level = 1; level <= alert.InitialLevel; level++)
            {
                var recipients = await alerts.GetEscalationRecipientsAsync(candidate.ObjectId, level, ct);
                await notifier.NotifyAsync(recipients, $"Vencimiento de {candidate.Code} ({alert.Severity})", Describe(candidate, alert), ct);
            }
        }
        return raised;
    }

    private static string Describe(MonitoringCandidate candidate, AlertCandidate alert) => alert.Kind == AlertKind.Expired
        ? $"El objeto {candidate.Code} ({candidate.ObjectType}) expiró el {candidate.ExpirationDate:yyyy-MM-dd}. Renuévelo o desactívelo."
        : $"El objeto {candidate.Code} ({candidate.ObjectType}) vence el {candidate.ExpirationDate:yyyy-MM-dd} (umbral de {alert.ThresholdDays} días).";
}

/// <summary>US-023: escalamiento de alertas no reconocidas (RN-069, RN-070).</summary>
public sealed class AlertEscalationJob(IClock clock, IAlertRepository alerts, Notifier notifier, AuditLogger audit) : IBackgroundJob
{
    public string Name => "AlertEscalation";
    public TimeSpan Interval => TimeSpan.FromMinutes(30);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var escalated = 0;
        foreach (var alert in await alerts.GetEscalationCandidatesAsync(now, ct))
        {
            var next = EscalationPolicy.NextLevel(alert.EscalationLevel);
            if (!await alerts.EscalateAsync(alert.AlertId, next, now, ct))
                continue;
            escalated++;
            await audit.SuccessAsync(AuditActions.AlertEscalated, "Alert", alert.AlertId.ToString(),
                new { alert.ObjectId, alert.AlertKey, level = next }, ct);
            var recipients = await alerts.GetEscalationRecipientsAsync(alert.ObjectId, next, ct);
            await notifier.NotifyAsync(recipients, $"Alerta escalada a {EscalationPolicy.LevelName(next)}",
                $"Una alerta de vencimiento ({alert.Severity}) no fue reconocida a tiempo y se escaló a {EscalationPolicy.LevelName(next)}.", ct);
        }
        return escalated;
    }
}

/// <summary>RN-049 y RN-057: activa y revoca accesos temporales por ventana y expira solicitudes sin respuesta.</summary>
public sealed class AccessWindowJob(IClock clock, ITemporaryAccessRepository accesses, IAccessRequestRepository requests,
    IUserDirectory users, Notifier notifier, AuditLogger audit) : IBackgroundJob
{
    public string Name => "AccessWindow";
    public TimeSpan Interval => TimeSpan.FromSeconds(30);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var (activated, expired) = await accesses.ProcessWindowAsync(now, ct);
        foreach (var access in expired)
        {
            await audit.SuccessAsync(AuditActions.TemporaryAccessExpired, "TemporaryAccess", access.AccessId.ToString(),
                new { access.ObjectId, access.BeneficiaryId }, ct);
        }

        var expiredRequests = await requests.ExpirePendingAsync(now, ct);
        foreach (var request in expiredRequests)
        {
            await audit.SuccessAsync(AuditActions.AccessRequestExpired, "AccessRequest", request.RequestId.ToString(),
                new { request.Code, request.ObjectId }, ct);
            await notifier.NotifyAsync(await users.GetContactsAsync([request.RequesterId], ct), $"Solicitud {request.Code} expirada",
                $"Su solicitud {request.Code} no se resolvió en el plazo y expiró (RN-049).", ct);
        }
        return activated + expired.Count + expiredRequests.Count;
    }
}

/// <summary>RN-076: verificación periódica de la cadena de hashes. Una ruptura alerta a Seguridad.</summary>
public sealed class AuditChainJob(IClock clock, IAuditReader reader, IUserDirectory users, Notifier notifier, AuditLogger audit) : IBackgroundJob
{
    public string Name => "AuditChainVerification";
    public TimeSpan Interval => TimeSpan.FromHours(6);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var status = await reader.VerifyChainAsync(clock.UtcNow, ct);
        if (status.IsIntact)
        {
            await audit.SuccessAsync(AuditActions.AuditChainVerified, "AuditChain", null, new { status.EventsVerified }, ct);
        }
        else
        {
            await audit.RecordAsync(AuditActions.AuditChainBroken, "AuditChain", null, AuditResults.Failed,
                new { status.FirstBrokenSequence, status.EventsVerified }, ct);
            await notifier.NotifyAsync(await users.GetActiveByRoleAsync(SystemRoles.Security, ct), "ALERTA CRÍTICA: cadena de auditoría rota",
                $"La verificación detectó una ruptura en la secuencia {status.FirstBrokenSequence}. Investigue de inmediato (RN-076).", ct);
        }
        return (int)Math.Min(status.EventsVerified, int.MaxValue);
    }
}

/// <summary>Despacho de la cola de notificaciones con reintentos (RN-099).</summary>
public sealed class NotificationDispatchJob(IClock clock, INotificationOutbox outbox, INotificationSender sender) : IBackgroundJob
{
    public const int MaxAttempts = 5;

    public string Name => "NotificationDispatch";
    public TimeSpan Interval => TimeSpan.FromMinutes(1);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var pending = await outbox.GetPendingAsync(50, MaxAttempts, ct);
        foreach (var message in pending)
        {
            if (!sender.IsEnabled)
            {
                await outbox.MarkResultAsync(message.Id, "Suppressed", "Canal de correo deshabilitado en la configuración.", clock.UtcNow, ct);
                continue;
            }
            try
            {
                await sender.SendAsync(message.Recipient, message.Subject, message.Body, ct);
                await outbox.MarkResultAsync(message.Id, "Sent", null, clock.UtcNow, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var status = message.Attempts + 1 >= MaxAttempts ? "Failed" : "Retry";
                await outbox.MarkResultAsync(message.Id, status, ex.GetType().Name, clock.UtcNow, ct);
            }
        }
        return pending.Count;
    }
}

