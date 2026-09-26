using System.Globalization;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Domain.Expiration;

/// <summary>Alerta que debe existir para un objeto en un momento dado.</summary>
public sealed record AlertCandidate(string AlertKey, AlertKind Kind, int? ThresholdDays, AlertSeverity Severity, byte InitialLevel);

/// <summary>Política de expiración aplicable (RN-062).</summary>
public sealed record ExpirationPolicyDefinition(Guid PolicyId, string Name, ObjectType? ObjectType, Criticality? Criticality,
    ThresholdSchedule Thresholds, bool IsActive);

public static class ExpirationEvaluator
{
    /// <summary>
    /// Devuelve la alerta del umbral vigente (el menor umbral alcanzado) o la alerta diaria de expirado (RN-068).
    /// La clave hace idempotente la generación (RN-064).
    /// </summary>
    public static AlertCandidate? Evaluate(DateTime expirationUtc, Criticality criticality, ThresholdSchedule schedule, DateTime nowUtc)
    {
        if (expirationUtc <= nowUtc)
        {
            var key = "EXP-" + nowUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return new AlertCandidate(key, AlertKind.Expired, null, AlertSeverity.Critical, InitialLevel(criticality, 0, expired: true));
        }

        var daysRemaining = (int)Math.Ceiling((expirationUtc - nowUtc).TotalDays);
        var reached = schedule.Days.Where(t => daysRemaining <= t).DefaultIfEmpty(-1).Min();
        if (reached < 0)
            return null;

        var severity = Severity(daysRemaining, criticality);
        return new AlertCandidate("T" + reached.ToString(CultureInfo.InvariantCulture), AlertKind.Threshold, reached, severity,
            InitialLevel(criticality, daysRemaining, expired: false));
    }

    /// <summary>RN-065.</summary>
    public static AlertSeverity Severity(int daysRemaining, Criticality criticality) => daysRemaining switch
    {
        <= 0 => AlertSeverity.Critical,
        <= 7 => AlertSeverity.High,
        <= 30 when criticality == Criticality.Critical => AlertSeverity.High,
        <= 30 => AlertSeverity.Medium,
        _ => AlertSeverity.Low,
    };

    /// <summary>RN-071: los Críticos a 30 días o menos y todo expirado se notifican directamente hasta N2 (el jefe y los Responsables; IMP-62).</summary>
    public static byte InitialLevel(Criticality criticality, int daysRemaining, bool expired) =>
        expired || (criticality == Criticality.Critical && daysRemaining <= 30) ? (byte)2 : (byte)1;

    /// <summary>La política más específica gana: tipo + criticidad &gt; tipo &gt; criticidad &gt; global (RN-062).</summary>
    public static ExpirationPolicyDefinition? SelectPolicy(IEnumerable<ExpirationPolicyDefinition> policies, ObjectType type, Criticality criticality)
    {
        return policies
            .Where(p => p.IsActive && (p.ObjectType is null || p.ObjectType == type) && (p.Criticality is null || p.Criticality == criticality))
            .OrderByDescending(p => (p.ObjectType is not null ? 2 : 0) + (p.Criticality is not null ? 1 : 0))
            .FirstOrDefault();
    }
}
