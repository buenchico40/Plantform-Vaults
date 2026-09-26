namespace PlatformVault.Domain.Expiration;

/// <summary>Escalamiento de alertas no reconocidas (RN-069, RN-070). Tres niveles con propietario único (IMP-62).</summary>
public static class EscalationPolicy
{
    public const byte MaxLevel = 3;

    /// <summary>Plazo sin reconocimiento antes de escalar. Crítica usa el plazo de Alta (supuesto IMP-13).</summary>
    public static TimeSpan WindowFor(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical or AlertSeverity.High => TimeSpan.FromHours(24),
        AlertSeverity.Medium => TimeSpan.FromHours(72),
        _ => TimeSpan.FromDays(7),
    };

    public static byte NextLevel(byte current) => current >= MaxLevel ? MaxLevel : (byte)(current + 1);

    public static string LevelName(byte level) => level switch
    {
        1 => "N1 · Propietario",
        2 => "N2 · Responsable jerárquico y Responsables de los grupos",
        _ => "N3 · Seguridad de la Información",
    };
}
