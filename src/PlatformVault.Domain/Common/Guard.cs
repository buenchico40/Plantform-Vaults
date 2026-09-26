namespace PlatformVault.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException(DomainErrors.InvalidValue, $"El campo {field} es obligatorio.");
        if (trimmed.Length > maxLength)
            throw new DomainException(DomainErrors.InvalidValue, $"El campo {field} admite como máximo {maxLength} caracteres.");
        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > maxLength)
            throw new DomainException(DomainErrors.InvalidValue, $"El campo {field} admite como máximo {maxLength} caracteres.");
        return trimmed;
    }

    public static string Reason(string? reason)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < 5)
            throw new DomainException(DomainErrors.ReasonRequired, "El motivo es obligatorio (mínimo 5 caracteres).");
        if (trimmed.Length > 500)
            throw new DomainException(DomainErrors.InvalidValue, "El motivo admite como máximo 500 caracteres.");
        return trimmed;
    }
}
