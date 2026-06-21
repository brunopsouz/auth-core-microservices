using System.Diagnostics;
using System.Runtime.ExceptionServices;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using AuthCore.Infrastructure.Observability;
using AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Connections;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Write.PostgreSQL.UnitOfWork;

/// <summary>
/// Representa unidade de trabalho transacional do PostgreSQL.
/// </summary>
internal sealed class NpgsqlUnitOfWork : IUnitOfWork, IDatabaseSession, IAsyncDisposable
{
    /// <summary>
    /// Campo que armazena db connection factory.
    /// </summary>
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly DatabaseMetrics _metrics;

    private NpgsqlConnection? _connection;
    private Stopwatch? _transactionStopwatch;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="dbConnectionFactory">Fábrica de conexão com o banco de dados.</param>
    public NpgsqlUnitOfWork(IDbConnectionFactory dbConnectionFactory, DatabaseMetrics metrics)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _metrics = metrics;
    }


    /// <summary>
    /// Transação atual da sessão.
    /// </summary>
    public NpgsqlTransaction? CurrentTransaction { get; private set; }

    /// <summary>
    /// Operação para iniciar uma transação.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentTransaction is not null)
            return;

        var connection = await GetTransactionalConnectionAsync(cancellationToken);
        try
        {
            CurrentTransaction = await connection.BeginTransactionAsync(cancellationToken);
            _transactionStopwatch = Stopwatch.StartNew();
        }
        catch
        {
            await ReleaseTransactionAndConnectionAsync(suppressErrors: true);
            throw;
        }
    }

    /// <summary>
    /// Operação para confirmar a transação atual.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentTransaction is null)
            return;

        try
        {
            await CurrentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await ReleaseTransactionAndConnectionAsync(suppressErrors: true);
            throw;
        }

        await ReleaseTransactionAndConnectionAsync();
    }

    /// <summary>
    /// Operação para desfazer a transação atual.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentTransaction is null)
            return;

        try
        {
            await CurrentTransaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            await ReleaseTransactionAndConnectionAsync(suppressErrors: true);
            throw;
        }

        await ReleaseTransactionAndConnectionAsync();
    }

    /// <summary>
    /// Operação para obter uma conexão aberta da sessão.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Conexão aberta pronta para uso.</returns>
    public async ValueTask<IDatabaseConnectionLease> AcquireConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentTransaction is not null && _connection is not null)
            return new DatabaseConnectionLease(_connection, ownsConnection: false, _metrics);

        var connection = (NpgsqlConnection)await _dbConnectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return new DatabaseConnectionLease(connection, ownsConnection: true, _metrics);
    }

    /// <summary>
    /// Operação para liberar os recursos da sessão.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await ReleaseTransactionAndConnectionAsync();
    }

    private async Task<NpgsqlConnection> GetTransactionalConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            return _connection;

        _connection = (NpgsqlConnection)await _dbConnectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return _connection;
    }

    private async ValueTask ReleaseTransactionAndConnectionAsync(bool suppressErrors = false)
    {
        Exception? cleanupException = null;

        if (CurrentTransaction is not null)
        {
            try
            {
                await CurrentTransaction.DisposeAsync();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }
            finally
            {
                CurrentTransaction = null;
            }
        }

        if (_connection is not null)
        {
            try
            {
                await _connection.DisposeAsync();
            }
            catch (Exception exception) when (cleanupException is not null || suppressErrors)
            {
                cleanupException ??= exception;
            }
            finally
            {
                _connection = null;
            }
        }

        if (_transactionStopwatch is not null)
        {
            _transactionStopwatch.Stop();
            _metrics.RecordTransaction(_transactionStopwatch.Elapsed);
            _transactionStopwatch = null;
        }

        if (cleanupException is not null && !suppressErrors)
            ExceptionDispatchInfo.Capture(cleanupException).Throw();
    }
}
