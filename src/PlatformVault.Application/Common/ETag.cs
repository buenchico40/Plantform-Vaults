using PlatformVault.Application.Abstractions;

namespace PlatformVault.Application.Common;

/// <summary>Versión de fila expuesta como ETag opaco (concurrencia optimista).</summary>
public static class ETag
{
    public static string From(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static byte[] Parse(string? etag)
    {
        var value = etag?.Trim().Trim('"');
        if (string.IsNullOrEmpty(value))
            throw new ValidationFailure("Se requiere la versión del recurso (cabecera If-Match).");
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length == 8 ? bytes : throw new FormatException();
        }
        catch (FormatException)
        {
            throw new ValidationFailure("La versión del recurso no es válida.");
        }
    }
}
