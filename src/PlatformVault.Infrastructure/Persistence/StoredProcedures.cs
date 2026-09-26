using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using PlatformVault.Application.Abstractions;

namespace PlatformVault.Infrastructure.Persistence;

/// <summary>
/// Único punto de acceso a SQL Server: solo ejecuta procedimientos almacenados (IMP-03). No existe SQL embebido
/// ni dinámico en C#. Traduce los errores de negocio lanzados por los procedimientos (THROW 51001-51003).
/// </summary>
public sealed class StoredProcedures(DbSession session)
{
    public async Task<int> ExecuteAsync(string procedure, object? parameters, CancellationToken ct)
    {
        var connection = await session.GetConnectionAsync(ct);
        return await Translate(() => connection.ExecuteAsync(Command(procedure, parameters, ct)));
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string procedure, object? parameters, CancellationToken ct)
    {
        var connection = await session.GetConnectionAsync(ct);
        var rows = await Translate(() => connection.QueryAsync<T>(Command(procedure, parameters, ct)));
        return rows.AsList();
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string procedure, object? parameters, CancellationToken ct)
    {
        var connection = await session.GetConnectionAsync(ct);
        return await Translate(() => connection.QueryFirstOrDefaultAsync<T>(Command(procedure, parameters, ct)));
    }

    public async Task<TResult> QueryMultipleAsync<TResult>(string procedure, object? parameters,
        Func<SqlMapper.GridReader, Task<TResult>> read, CancellationToken ct)
    {
        var connection = await session.GetConnectionAsync(ct);
        return await Translate(async () =>
        {
            await using var grid = await connection.QueryMultipleAsync(Command(procedure, parameters, ct));
            return await read(grid);
        });
    }

    /// <summary>Parámetro de tabla app.GuidList.</summary>
    public static SqlMapper.ICustomQueryParameter GuidList(IEnumerable<Guid> ids)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        foreach (var id in ids.Distinct())
            table.Rows.Add(id);
        return table.AsTableValuedParameter("app.GuidList");
    }

    private CommandDefinition Command(string procedure, object? parameters, CancellationToken ct) =>
        new(procedure, parameters, session.Transaction, session.CommandTimeout, CommandType.StoredProcedure, cancellationToken: ct);

    private static async Task<T> Translate<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (SqlException ex) when (ex.Number == 51001)
        {
            throw new ConcurrencyFailure();
        }
        catch (SqlException ex) when (ex.Number == 51002)
        {
            throw new NotFoundFailure(Detail(ex.Message));
        }
        catch (SqlException ex) when (ex.Number == 51003)
        {
            throw new ConflictFailure("INVALID_STATE", StateMessage(Detail(ex.Message)));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new ConflictFailure("DUPLICATE", "Ya existe un registro con esos datos.");
        }
    }

    private static string Detail(string message)
    {
        var index = message.IndexOf('|', StringComparison.Ordinal);
        return index < 0 ? message : message[(index + 1)..];
    }

    private static string StateMessage(string detail) => detail switch
    {
        "request_not_pending" => "La solicitud ya no está pendiente.",
        "access_not_active" => "El acceso temporal ya no está vigente.",
        "alert_not_open" => "La alerta ya fue reconocida o resuelta.",
        "last_responsible" => "El grupo debe conservar al menos un Responsable (RN-034).",
        _ => "La operación no es válida en el estado actual del recurso.",
    };
}

internal static class DbValues
{
    public static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? Utc(DateTime? value) => value is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;

    public static TEnum Enum<TEnum>(string value) where TEnum : struct, System.Enum => System.Enum.Parse<TEnum>(value, ignoreCase: false);

    public static TEnum? OptionalEnum<TEnum>(string? value) where TEnum : struct, System.Enum =>
        value is null ? null : System.Enum.Parse<TEnum>(value, ignoreCase: false);

    public static IReadOnlyList<string> Split(string? csv) =>
        string.IsNullOrEmpty(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
