using Npgsql;

namespace AuthCore.Infrastructure.Abstractions.Data;

/// <summary>
/// Define operações para obter a sessão atual de banco de dados.
/// </summary>
public interface IDatabaseSession
{
    /// <summary>
    /// Transação atual da sessão.
    /// </summary>
    NpgsqlTransaction? CurrentTransaction { get; }

    /// <summary>
    /// Operação para adquirir uma conexão aberta durante o tempo necessário.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Lease que controla a devolução da conexão ao pool.</returns>
    ValueTask<IDatabaseConnectionLease> AcquireConnectionAsync(CancellationToken cancellationToken = default);
}
