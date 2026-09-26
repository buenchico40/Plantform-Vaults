using System.Globalization;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Domain.Expiration;

/// <summary>Umbrales de aviso en días, de mayor a menor (RN-061).</summary>
public sealed class ThresholdSchedule
{
    public static readonly IReadOnlyList<int> Default = [180, 120, 90, 60, 30, 15, 7, 1];
    private static readonly int[] MandatoryForCriticalOrHigh = [30, 7, 1];

    private ThresholdSchedule(IReadOnlyList<int> days) => Days = days;

    public IReadOnlyList<int> Days { get; }

    public static ThresholdSchedule Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException(DomainErrors.InvalidThresholds, "Debe indicar al menos un umbral.");
        var days = new List<int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var d) || d is < 1 or > 365)
                throw new DomainException(DomainErrors.InvalidThresholds, $"Umbral no válido: '{part}'. Use días entre 1 y 365.");
            days.Add(d);
        }
        return new ThresholdSchedule(days.Distinct().OrderByDescending(d => d).ToList());
    }

    /// <summary>Una política que alcanza objetos Críticos o Altos no puede quitar 30, 7 ni 1 días (RN-061).</summary>
    public ThresholdSchedule ValidateFor(Criticality? criticality)
    {
        if (criticality is null or Criticality.Critical or Criticality.High)
        {
            var missing = MandatoryForCriticalOrHigh.Where(m => !Days.Contains(m)).ToList();
            if (missing.Count > 0)
                throw new DomainException(DomainErrors.InvalidThresholds,
                    $"Los umbrales 30, 7 y 1 son obligatorios para objetos Críticos o Altos (faltan: {string.Join(", ", missing)}).");
        }
        return this;
    }

    public override string ToString() => string.Join(',', Days.Select(d => d.ToString(CultureInfo.InvariantCulture)));
}
