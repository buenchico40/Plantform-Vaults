using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PlatformVault.Application.Abstractions;

namespace PlatformVault.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Cadena de conexión de la cuenta técnica única (IMP-12). Solo la usa PlatformVault.Api.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Conexión y transacción de la petición. Todos los repositorios la comparten, de modo que el cambio de negocio,
/// el valor cifrado y la auditoría se confirman o revierten juntos (RN-079, IMP-18).
/// </summary>
public sealed class DbSession(IOptions<DatabaseOptions> options) : IUnitOfWork, IAsyncDisposable, IDisposable
{
    public void Dispose()
    {
        Transaction?.Dispose();
        Transaction = null;
        _connection?.Dispose();
        _connection = null;
    }

    private SqlConnection? _connection;

    public SqlTransaction? Transaction { get; private set; }

    public int CommandTimeout => options.Value.CommandTimeoutSeconds;

    public async Task<SqlConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is null)
        {
            _connection = new SqlConnection(options.Value.ConnectionString);
        }
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);
        return _connection;
    }

    public async Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken)
    {
        if (Transaction is not null)
            throw new InvalidOperationException("Ya hay una transacción activa en esta petición.");
        var connection = await GetConnectionAsync(cancellationToken);
        Transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        return new Scope(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
            Transaction = null;
        }
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private sealed class Scope(DbSession session) : ITransactionScope
    {
        private bool _completed;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await session.Transaction!.CommitAsync(cancellationToken);
            _completed = true;
            await ReleaseAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed && session.Transaction is not null)
            {
                try
                {
                    await session.Transaction.RollbackAsync();
                }
                catch (InvalidOperationException)
                {
                    // La transacción ya fue revertida por SQL Server (XACT_ABORT).
                }
            }
            await ReleaseAsync();
        }

        private async Task ReleaseAsync()
        {
            if (session.Transaction is not null)
            {
                await session.Transaction.DisposeAsync();
                session.Transaction = null;
            }
        }
    }
}
