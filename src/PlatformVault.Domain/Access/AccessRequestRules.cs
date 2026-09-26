using PlatformVault.Domain.Common;
using PlatformVault.Domain.Objects;
using PlatformVault.Domain.Security;

namespace PlatformVault.Domain.Access;

/// <summary>Solicitud validada, lista para registrarse.</summary>
public sealed record ValidatedAccessRequest(
    AccessAction Action,
    string Justification,
    DateTime StartUtc,
    int DurationMinutes,
    ApproverKind ApproverKind,
    DateTime PendingExpiresAtUtc);

/// <summary>Reglas de solicitud (RN-047 a RN-050) y de decisión (RN-051, RN-054).</summary>
public static class AccessRequestRules
{
    public const int MinJustificationLength = 20;
    public static readonly TimeSpan MaxScheduleAhead = TimeSpan.FromDays(30);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public static ValidatedAccessRequest ValidateNew(ManagedObject obj, AccessAction action, string? justification,
        DateTime? requestedStartUtc, int durationMinutes, DateTime nowUtc)
    {
        if (obj.LifecycleState != LifecycleState.Active)
            throw new DomainException(DomainErrors.ObjectNotActive, "Solo se admiten solicitudes sobre objetos activos (RN-050).");
        if (!obj.HasPayload)
            throw new DomainException(DomainErrors.ActionNotApplicable, "El objeto no tiene un valor custodiado.");

        var payloadKind = obj.EnsureCanStorePayload();
        if (AccessPolicy.ActionFor(payloadKind) != action)
            throw new DomainException(DomainErrors.ActionNotApplicable, $"La acción {action} no corresponde al tipo de valor del objeto.");

        var text = justification?.Trim() ?? string.Empty;
        if (text.Length < MinJustificationLength)
            throw new DomainException(DomainErrors.JustificationTooShort, $"La justificación debe tener al menos {MinJustificationLength} caracteres (RN-047).");
        if (text.Length > 1000)
            throw new DomainException(DomainErrors.InvalidValue, "La justificación admite como máximo 1000 caracteres.");
        if (CardDataDetector.ContainsCardNumber(text))
            throw new DomainException(DomainErrors.CardDataDetected, "La justificación no puede contener datos de tarjeta (RN-124).");

        var rule = AccessPolicy.Resolve(obj.Criticality, obj.Sensitivity);
        if (durationMinutes <= 0 || TimeSpan.FromMinutes(durationMinutes) > rule.MaxDuration)
            throw new DomainException(DomainErrors.DurationExceeded,
                $"La duración debe estar entre 1 y {rule.MaxDuration.TotalMinutes:0} minutos (RN-048).");

        var start = requestedStartUtc ?? nowUtc;
        if (start < nowUtc - ClockSkew || start > nowUtc + MaxScheduleAhead)
            throw new DomainException(DomainErrors.InvalidValue, "El inicio del acceso debe estar entre ahora y los próximos 30 días.");
        if (start < nowUtc) start = nowUtc;

        return new ValidatedAccessRequest(action, text, start, durationMinutes, rule.ApproverKind, nowUtc + AccessPolicy.PendingLifetime);
    }

    public static string? ValidateDecision(Guid approverId, Guid requesterId, Decision decision, string? comment)
    {
        if (approverId == requesterId)
            throw new DomainException(DomainErrors.SelfApproval, "Nadie puede aprobar su propia solicitud (RN-054).");
        var clean = comment?.Trim();
        if (decision == Decision.Rejected && string.IsNullOrEmpty(clean))
            throw new DomainException(DomainErrors.CommentRequired, "El comentario es obligatorio al rechazar (RN-051).");
        if (clean is { Length: > 1000 })
            throw new DomainException(DomainErrors.InvalidValue, "El comentario admite como máximo 1000 caracteres.");
        if (CardDataDetector.ContainsCardNumber(clean))
            throw new DomainException(DomainErrors.CardDataDetected, "El comentario no puede contener datos de tarjeta (RN-124).");
        return string.IsNullOrEmpty(clean) ? null : clean;
    }

    /// <summary>Ventana efectiva del acceso aprobado: si el inicio pedido ya pasó, empieza al aprobarse.</summary>
    public static (DateTime Start, DateTime End) AccessWindow(DateTime requestedStartUtc, int durationMinutes, DateTime nowUtc)
    {
        var start = requestedStartUtc < nowUtc ? nowUtc : requestedStartUtc;
        return (start, start.AddMinutes(durationMinutes));
    }
}
