using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Access;
using PlatformVault.Domain.Identity;

namespace PlatformVault.Application.Notifications;

/// <summary>
/// Notificaciones de negocio. Solo metadatos y el código del objeto, nunca valores (RN-067).
/// </summary>
public sealed class Notifier(INotificationQueue queue, IUserDirectory users, IGroupRepository groups, ICurrentUser currentUser)
{
    /// <summary>RN-110: aviso inmediato a Seguridad de cambios sobre objetos Críticos.</summary>
    public async Task NotifySecurityAsync(bool critical, string objectCode, string what, CancellationToken ct)
    {
        if (!critical) return; // El resumen diario de objetos no críticos pertenece a la iteración 1B.
        var recipients = await users.GetActiveByRoleAsync(SystemRoles.Security, ct);
        await queue.EnqueueAsync(recipients, $"[PlatformVault] Objeto crítico {objectCode}: {what}",
            $"El usuario {currentUser.DisplayName} realizó la acción «{what}» sobre el objeto crítico {objectCode}.", ct);
    }

    /// <summary>RN-105: aviso a los demás miembros de los grupos del objeto (sin el autor, un mensaje por persona).</summary>
    public async Task NotifyGroupActivityAsync(Guid objectId, string objectCode, string what, CancellationToken ct)
    {
        var members = await groups.GetMemberContactsForObjectAsync(objectId, ct);
        var recipients = members.Where(m => m.UserId != currentUser.UserId).DistinctBy(m => m.UserId).ToList();
        await queue.EnqueueAsync(recipients, $"[PlatformVault] Actividad en {objectCode}",
            $"{currentUser.DisplayName} realizó la acción «{what}» sobre el objeto {objectCode}.", ct);
    }

    public Task NotifyAsync(IEnumerable<UserContact> recipients, string subject, string body, CancellationToken ct) =>
        queue.EnqueueAsync(recipients, "[PlatformVault] " + subject, body, ct);
}
