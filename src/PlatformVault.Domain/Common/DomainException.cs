namespace PlatformVault.Domain.Common;

/// <summary>
/// Violación de una regla de negocio. <see cref="Code"/> es estable y se devuelve al cliente;
/// <see cref="Exception.Message"/> es legible y nunca contiene valores sensibles.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message) : base(message) => Code = code;

    public DomainException() : this(DomainErrors.RuleViolation, "Regla de negocio incumplida.") { }

    public DomainException(string message) : this(DomainErrors.RuleViolation, message) { }

    public DomainException(string message, Exception innerException) : base(message, innerException) => Code = DomainErrors.RuleViolation;

    public string Code { get; }
}
