using Npgsql;

namespace NotificationCore.Infrastructure.Abstractions.Data;

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
    /// Operação para obter uma conexão aberta da sessão.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Conexão aberta pronta para uso.</returns>
    Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default);
}
