using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.UnitOfWork;

/// <summary>
/// Representa unidade de trabalho transacional do PostgreSQL.
/// </summary>
public sealed class NpgsqlUnitOfWork : IUnitOfWork, IDatabaseSession, IAsyncDisposable
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    private NpgsqlConnection? _connection;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="dbConnectionFactory">Fábrica de conexão com o banco de dados.</param>
    public NpgsqlUnitOfWork(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    #endregion

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

        var connection = await GetOpenConnectionAsync(cancellationToken);
        CurrentTransaction = await connection.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Operação para confirmar a transação atual.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentTransaction is null)
            return;

        await CurrentTransaction.CommitAsync(cancellationToken);
        await CurrentTransaction.DisposeAsync();
        CurrentTransaction = null;
    }

    /// <summary>
    /// Operação para desfazer a transação atual.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentTransaction is null)
            return;

        await CurrentTransaction.RollbackAsync(cancellationToken);
        await CurrentTransaction.DisposeAsync();
        CurrentTransaction = null;
    }

    /// <summary>
    /// Operação para obter uma conexão aberta da sessão.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Conexão aberta pronta para uso.</returns>
    public async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
            return _connection;

        var connection = await _dbConnectionFactory.CreateOpenConnectionAsync(cancellationToken);
        _connection = (NpgsqlConnection)connection;

        return _connection;
    }

    /// <summary>
    /// Operação para liberar os recursos da sessão.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (CurrentTransaction is not null)
        {
            await CurrentTransaction.DisposeAsync();
            CurrentTransaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
