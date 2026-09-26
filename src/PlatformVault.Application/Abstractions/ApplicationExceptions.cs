namespace PlatformVault.Application.Abstractions;

/// <summary>Base de los errores de aplicación. La API los traduce a ProblemDetails sin detalle interno.</summary>
public abstract class ApplicationFailure : Exception
{
    protected ApplicationFailure(string code, string message) : base(message) => Code = code;

    public string Code { get; }
}

/// <summary>404. También se usa para objetos fuera del ámbito del usuario, para no confirmar su existencia (IMP-08).</summary>
public sealed class NotFoundFailure(string resource) : ApplicationFailure("NOT_FOUND", $"No se encontró el recurso {resource}.");

/// <summary>403: el recurso es visible pero la acción no está permitida.</summary>
public sealed class ForbiddenFailure(string message = "No tiene permiso para realizar esta acción.") : ApplicationFailure("FORBIDDEN", message);

/// <summary>409: el recurso cambió desde que se leyó (control de concurrencia optimista).</summary>
public sealed class ConcurrencyFailure() : ApplicationFailure("CONCURRENCY_CONFLICT", "El recurso fue modificado por otro usuario. Recárguelo e intente de nuevo.");

/// <summary>409: conflicto con el estado actual (duplicados, estados no válidos).</summary>
public sealed class ConflictFailure(string code, string message) : ApplicationFailure(code, message);

/// <summary>400: entrada no válida.</summary>
public sealed class ValidationFailure(string message, IReadOnlyDictionary<string, string[]>? errors = null)
    : ApplicationFailure("VALIDATION_ERROR", message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}

/// <summary>401 con código específico: la acción exige re-autenticación reciente (IMP-29).</summary>
public sealed class ReauthenticationRequiredFailure()
    : ApplicationFailure("REAUTHENTICATION_REQUIRED", "Confirme su contraseña para continuar (re-autenticación en los últimos 15 minutos).");

/// <summary>424: no se pudo registrar la auditoría y la operación se rechaza (fail-closed, RN-079).</summary>
public sealed class AuditUnavailableFailure(Exception inner)
    : ApplicationFailure("AUDIT_UNAVAILABLE", "No se pudo registrar la auditoría; la operación se rechazó.")
{
    public Exception Inner { get; } = inner;
}

/// <summary>401: credenciales no válidas. El mensaje no revela si el usuario existe.</summary>
public sealed class AuthenticationFailure(string message = "Usuario o contraseña incorrectos.") : ApplicationFailure("INVALID_CREDENTIALS", message);
