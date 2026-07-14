using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NotificationCore.Infrastructure.Configurations;
using RabbitMQ.Client;

namespace NotificationCore.Api.HealthChecks;

/// <summary>
/// Representa health check para a conectividade com o RabbitMQ.
/// </summary>
internal sealed class RabbitMqHealthCheck : IHealthCheck
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Campo que armazena options.
    /// </summary>
    private readonly RabbitMqOptions _options;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="options">Opções de conexão com RabbitMQ.</param>
    public RabbitMqHealthCheck(IOptions<RabbitMqOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    /// <summary>
    /// Operação para verificar a saúde da conectividade com o RabbitMQ.
    /// </summary>
    /// <param name="context">Contexto da execução do health check.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Resultado do health check executado.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.Enabled)
            return HealthCheckResult.Healthy();

        try
        {
            await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var factory = CreateConnectionFactory();
                        using var connection = factory.CreateConnection();
                        using var channel = connection.CreateModel();

                        if (!connection.IsOpen || !channel.IsOpen)
                            throw new InvalidOperationException("RabbitMQ health check failed.");
                    },
                    cancellationToken)
                .WaitAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch
        {
            return new HealthCheckResult(context.Registration.FailureStatus);
        }
    }

    private ConnectionFactory CreateConnectionFactory()
    {
        return new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.Username,
            Password = _options.Password,
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = ConnectionTimeout,
            SocketReadTimeout = ConnectionTimeout,
            SocketWriteTimeout = ConnectionTimeout
        };
    }
}
