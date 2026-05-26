using System.Data;
using Microsoft.Extensions.Options;
using NotificationCore.Infrastructure.Abstractions.Data;
using NotificationCore.Infrastructure.Configurations;
using Npgsql;

namespace NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.Connections;

/// <summary>
/// Representa a fábrica de conexões PostgreSQL.
/// </summary>
internal sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    /// <summary>
    /// Campo que armazena connection string.
    /// </summary>
    private readonly string _connectionString;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="options">Opções de configuração do banco de dados.</param>
    public NpgsqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _connectionString = options.Value.PostgreSql;

        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("Database connection string was not configured.");
    }


    /// <summary>
    /// Operação para criar uma conexão aberta com o banco de dados.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Conexão aberta pronta para uso.</returns>
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
