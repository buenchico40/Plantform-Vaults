namespace PlatformVault.Application.Abstractions;

// CQRS con interfaces propias resueltas por inyección de dependencias (IMP-37, sin MediatR).

public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct);
}

/// <summary>Resultado vacío para comandos sin respuesta.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public sealed record PageRequest(int Page = 1, int PageSize = 25)
{
    public const int MaxPageSize = 200;

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 ? 25 : Math.Min(PageSize, MaxPageSize);
    public int Offset => (SafePage - 1) * SafePageSize;
}
