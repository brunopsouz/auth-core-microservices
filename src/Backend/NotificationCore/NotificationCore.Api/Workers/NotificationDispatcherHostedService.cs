using Microsoft.Extensions.Options;
using NotificationCore.Application.Notifications.UseCases.DispatchPendingNotification;
using NotificationCore.Infrastructure.Configurations;

namespace NotificationCore.Api.Workers;

/// <summary>
/// Representa worker hospedado para despacho de notificações pendentes.
/// </summary>
public sealed class NotificationDispatcherHostedService : BackgroundService
{
    private readonly ILogger<NotificationDispatcherHostedService> _logger;
    private readonly NotificationDispatcherOptions _notificationDispatcherOptions;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="serviceScopeFactory">Fábrica de escopos da aplicação.</param>
    /// <param name="notificationDispatcherOptions">Opções do despachante de notificações.</param>
    /// <param name="logger">Serviço de logging.</param>
    public NotificationDispatcherHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<NotificationDispatcherOptions> notificationDispatcherOptions,
        ILogger<NotificationDispatcherHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);
        ArgumentNullException.ThrowIfNull(notificationDispatcherOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _serviceScopeFactory = serviceScopeFactory;
        _notificationDispatcherOptions = notificationDispatcherOptions.Value;
        _logger = logger;
    }

    #endregion

    /// <summary>
    /// Operação para executar o worker de despacho de notificações.
    /// </summary>
    /// <param name="stoppingToken">Token para cancelamento da execução.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_notificationDispatcherOptions.Enabled)
        {
            _logger.LogInformation("Worker de despacho de notificações desabilitado por configuração.");
            return;
        }

        _logger.LogInformation("Worker de despacho de notificações iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<IDispatchPendingNotificationUseCase>();
                var result = await useCase.Execute(CreateCommand());

                _logger.LogInformation(
                    "Ciclo de despacho de notificações concluído. Found={Found}, Sent={Sent}, RetryScheduled={RetryScheduled}, DeadLettered={DeadLettered}.",
                    result.Found,
                    result.Sent,
                    result.RetryScheduled,
                    result.DeadLettered);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Falha no ciclo do worker de despacho de notificações.");
            }

            await DelayUntilNextCycleAsync(stoppingToken);
        }

        _logger.LogInformation("Worker de despacho de notificações encerrado.");
    }

    #region Helpers

    /// <summary>
    /// Operação para criar comando de despacho.
    /// </summary>
    /// <returns>Comando de despacho configurado.</returns>
    private DispatchPendingNotificationCommand CreateCommand()
    {
        return new DispatchPendingNotificationCommand
        {
            DueAtUtc = DateTime.UtcNow,
            Take = _notificationDispatcherOptions.BatchSize,
            RetryDelay = TimeSpan.FromSeconds(_notificationDispatcherOptions.RetryDelaySeconds),
            ProcessingTimeout = TimeSpan.FromSeconds(_notificationDispatcherOptions.ProcessingTimeoutSeconds)
        };
    }

    /// <summary>
    /// Operação para aguardar o próximo ciclo de processamento.
    /// </summary>
    /// <param name="stoppingToken">Token para cancelamento da espera.</param>
    private Task DelayUntilNextCycleAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(_notificationDispatcherOptions.PollingIntervalSeconds);

        return Task.Delay(delay, stoppingToken);
    }

    #endregion
}
