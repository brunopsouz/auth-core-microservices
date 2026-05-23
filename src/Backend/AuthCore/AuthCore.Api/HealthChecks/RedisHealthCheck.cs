using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace AuthCore.Api.HealthChecks;

/// <summary>
/// Representa health check para a conectividade com o Redis.
/// </summary>
internal sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="connectionMultiplexer">Conexão compartilhada com o Redis.</param>
    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    #endregion

    /// <summary>
    /// Operação para verificar a saúde da conectividade com o Redis.
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
            if (!_connectionMultiplexer.IsConnected)
                return HealthCheckResult.Unhealthy("Redis não está conectado.");

            await _connectionMultiplexer.GetDatabase().PingAsync();

            return HealthCheckResult.Healthy("Redis acessível.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao validar a conectividade com o Redis.", exception);
        }
    }
}
