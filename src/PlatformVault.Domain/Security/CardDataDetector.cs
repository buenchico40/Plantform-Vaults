namespace PlatformVault.Domain.Security;

/// <summary>
/// RN-124: detecta posibles números de tarjeta en texto libre. Una secuencia de dígitos (admite espacios o guiones
/// como separadores) de 13 a 19 dígitos que supera la verificación de Luhn se considera PAN.
/// Mantiene la plataforma fuera del CDE.
/// </summary>
public static class CardDataDetector
{
    public static bool ContainsCardNumber(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        Span<int> digits = stackalloc int[20];
        var count = 0;
        var overflow = false;

        for (var i = 0; i <= text.Length; i++)
        {
            var c = i < text.Length ? text[i] : '\0';
            if (char.IsAsciiDigit(c))
            {
                if (count < digits.Length) digits[count++] = c - '0';
                else overflow = true;
                continue;
            }

            var isSeparator = c is ' ' or '-';
            var nextIsDigit = i + 1 < text.Length && char.IsAsciiDigit(text[i + 1]);
            if (isSeparator && count > 0 && nextIsDigit)
                continue;

            if (!overflow && count is >= 13 and <= 19 && PassesLuhn(digits[..count]))
                return true;
            count = 0;
            overflow = false;
        }
        return false;
    }

    private static bool PassesLuhn(ReadOnlySpan<int> digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i];
            if (doubleIt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }
}
