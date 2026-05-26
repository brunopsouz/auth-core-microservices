using System.Data;
using AuthCore.Infrastructure.Abstractions.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AuthCore.Api.HealthChecks;

/// <summary>
/// Representa health check para a conectividade com o banco de dados.
/// </summary>
internal sealed class DatabaseHealthCheck : IHealthCheck
{
    /// <summary>
    /// Campo que armazena db connection factory.
    /// </summary>
    private readonly IDbConnectionFactory _dbConnectionFactory;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="dbConnectionFactory">Fábrica de conexões abertas do banco.</param>
    public DatabaseHealthCheck(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
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
            using var connection = await _dbConnectionFactory.CreateOpenConnectionAsync(cancellationToken);

            return connection.State == ConnectionState.Open
                ? HealthCheckResult.Healthy("Banco de dados acessível.")
                : HealthCheckResult.Unhealthy("Conexão com o banco de dados não foi aberta.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao validar a conectividade com o banco de dados.", exception);
        }
    }
}
