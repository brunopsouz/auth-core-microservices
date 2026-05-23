using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NotificationCore.Infrastructure.Configurations;

namespace NotificationCore.Api.HealthChecks;

/// <summary>
/// Representa health check para a configuração do despachante de notificações.
/// </summary>
internal sealed class NotificationDispatcherHealthCheck : IHealthCheck
{
    private readonly NotificationDispatcherOptions _options;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="options">Opções do despachante de notificações.</param>
    public NotificationDispatcherHealthCheck(IOptions<NotificationDispatcherOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    #endregion

    /// <summary>
    /// Operação para verificar a saúde do despachante de notificações.
    /// </summary>
    /// <param name="context">Contexto da execução do health check.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Resultado do health check executado.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Despachante de notificações desabilitado por configuração."));

        if (_options.BatchSize <= 0
            || _options.PollingIntervalSeconds <= 0
            || _options.RetryDelaySeconds <= 0
            || _options.ProcessingTimeoutSeconds <= 0)
            return Task.FromResult(HealthCheckResult.Unhealthy("Despachante de notificações possui configuração inválida."));

        return Task.FromResult(HealthCheckResult.Healthy("Despachante de notificações configurado."));
    }
}
