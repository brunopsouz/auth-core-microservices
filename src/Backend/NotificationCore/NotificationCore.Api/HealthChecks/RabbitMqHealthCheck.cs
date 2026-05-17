using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NotificationCore.Infrastructure.Configurations;
using RabbitMQ.Client;

namespace NotificationCore.Api.HealthChecks;

/// <summary>
/// Representa health check para a conectividade com o RabbitMQ.
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqOptions _options;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="options">Opções de conexão com RabbitMQ.</param>
    public RabbitMqHealthCheck(IOptions<RabbitMqOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    #endregion

    /// <summary>
    /// Operação para verificar a saúde da conectividade com o RabbitMQ.
    /// </summary>
    /// <param name="context">Contexto da execução do health check.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Resultado do health check executado.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ desabilitado por configuração."));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.Username,
                Password = _options.Password,
                AutomaticRecoveryEnabled = false
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            return Task.FromResult(
                connection.IsOpen && channel.IsOpen
                    ? HealthCheckResult.Healthy("RabbitMQ acessível.")
                    : HealthCheckResult.Unhealthy("Conexão com o RabbitMQ não foi aberta."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Falha ao validar a conectividade com o RabbitMQ.", exception));
        }
    }
}
