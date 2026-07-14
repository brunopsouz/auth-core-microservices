using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace AuthCore.Api.HealthChecks;

/// <summary>
/// Representa health check para a conectividade com o banco de dados.
/// </summary>
internal sealed class DatabaseHealthCheck : IHealthCheck
{
    /// <summary>
    /// Campo que armazena a fonte de dados PostgreSQL.
    /// </summary>
    private readonly NpgsqlDataSource _dataSource;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="dataSource">Fonte de dados PostgreSQL compartilhada.</param>
    public DatabaseHealthCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        _dataSource = dataSource;
    }


    /// <summary>
    /// Operação para verificar a saúde da conectividade com o banco.
    /// </summary>
    /// <param name="context">Contexto da execução do health check.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Resultado do health check executado.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";

            _ = await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch
        {
            return new HealthCheckResult(context.Registration.FailureStatus);
        }
    }
}
