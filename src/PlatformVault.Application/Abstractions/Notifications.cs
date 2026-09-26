using PlatformVault.Application.Access;

namespace PlatformVault.Application.Abstractions;

/// <summary>
/// Cola de notificaciones (IMP-16). Se encola en la misma transacción que el cambio y un trabajo la despacha.
/// Los mensajes nunca contienen valores sensibles (RN-067).
/// </summary>
public interface INotificationQueue
{
    Task EnqueueAsync(IEnumerable<UserContact> recipients, string subject, string body, CancellationToken ct);
}

/// <summary>Envío efectivo de un mensaje (SMTP en la Fase 1).</summary>
public interface INotificationSender
{
    bool IsEnabled { get; }
    Task SendAsync(string recipient, string subject, string body, CancellationToken ct);
}

public sealed record PendingNotification(long Id, string Channel, string Recipient, string Subject, string Body, int Attempts);

public interface INotificationOutbox
{
    Task<IReadOnlyList<PendingNotification>> GetPendingAsync(int top, int maxAttempts, CancellationToken ct);
    Task MarkResultAsync(long notificationId, string status, string? error, DateTime nowUtc, CancellationToken ct);
}
