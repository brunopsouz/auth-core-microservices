using System.Data;

namespace NotificationCore.Infrastructure.Abstractions.Data;

/// <summary>
/// Define operação para criar conexões de banco de dados abertas.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Operação para criar uma conexão aberta com o banco de dados.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Conexão aberta pronta para uso.</returns>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
