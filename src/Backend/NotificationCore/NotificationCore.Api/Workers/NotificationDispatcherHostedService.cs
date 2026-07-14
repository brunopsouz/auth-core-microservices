using System.Diagnostics;
using Microsoft.Extensions.Options;
using NotificationCore.Api.Observability;
using NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;
using NotificationCore.Infrastructure.Configurations;
using NotificationCore.Infrastructure.Observability;

namespace NotificationCore.Api.Workers;

/// <summary>
/// Representa worker hospedado para despacho de notificações pendentes.
/// </summary>
internal sealed class NotificationDispatcherHostedService : BackgroundService
{
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<NotificationDispatcherHostedService> _logger;
    /// <summary>
    /// Campo que armazena notification metrics.
    /// </summary>
    private readonly NotificationMetrics _notificationMetrics;
    /// <summary>
    /// Campo que armazena notification dispatch telemetry.
    /// </summary>
    private readonly NotificationDispatchTelemetry _notificationDispatchTelemetry;
    /// <summary>
    /// Campo que armazena notification dispatcher options.
    /// </summary>
    private readonly NotificationDispatcherOptions _notificationDispatcherOptions;
    /// <summary>
    /// Campo que armazena service scope factory.
    /// </summary>
    private readonly IServiceScopeFactory _serviceScopeFactory;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="serviceScopeFactory">Fábrica de escopos da aplicação.</param>
    /// <param name="notificationDispatcherOptions">Opções do despachante de notificações.</param>
    /// <param name="notificationMetrics">Métricas de notificações.</param>
    /// <param name="logger">Serviço de logging.</param>
    /// <param name="notificationDispatchTelemetry">Telemetria técnica do despacho.</param>
    public NotificationDispatcherHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<NotificationDispatcherOptions> notificationDispatcherOptions,
        NotificationMetrics notificationMetrics,
        NotificationDispatchTelemetry notificationDispatchTelemetry,
        ILogger<NotificationDispatcherHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);
        ArgumentNullException.ThrowIfNull(notificationDispatcherOptions);
        ArgumentNullException.ThrowIfNull(notificationMetrics);
        ArgumentNullException.ThrowIfNull(notificationDispatchTelemetry);
        ArgumentNullException.ThrowIfNull(logger);

        _serviceScopeFactory = serviceScopeFactory;
        _notificationDispatcherOptions = notificationDispatcherOptions.Value;
        _notificationMetrics = notificationMetrics;
        _notificationDispatchTelemetry = notificationDispatchTelemetry;
        _logger = logger;
    }


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
            Stopwatch? stopwatch = null;

            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                stopwatch = Stopwatch.StartNew();

                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<IDispatchPendingNotificationUseCase>();
                var contextReader = scope.ServiceProvider.GetRequiredService<NotificationDispatchTelemetryContextReader>();
                var command = CreateCommand(stoppingToken);
                var contexts = await contextReader.ReadAsync(command);
                var result = await _notificationDispatchTelemetry.TrackAsync(
                    contexts,
                    () => useCase.Execute(command),
                    stoppingToken);

                _notificationMetrics.RecordPending(result.Found);
                _notificationMetrics.RecordSent(result.Sent);
                _notificationMetrics.RecordFailed(result.DeadLettered);
                _notificationMetrics.RecordEmailVerificationRetries(result.EmailVerificationRetries);
                _notificationMetrics.RecordTestEmailRetries(result.TestEmailRetries);
                _notificationMetrics.RecordOtherTransactionalRetries(result.OtherTransactionalRetries);

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
            finally
            {
                if (stopwatch is not null)
                {
                    stopwatch.Stop();
                    _notificationMetrics.RecordDispatchDuration(stopwatch.Elapsed);
                }
            }

            await DelayUntilNextCycleAsync(stoppingToken);
        }

        _logger.LogInformation("Worker de despacho de notificações encerrado.");
    }


    /// <summary>
    /// Operação para criar comando de despacho.
    /// </summary>
    /// <returns>Comando de despacho configurado.</returns>
    private DispatchPendingNotificationCommand CreateCommand(CancellationToken stoppingToken)
    {
        return new DispatchPendingNotificationCommand
        {
            DueAtUtc = DateTime.UtcNow,
            Take = _notificationDispatcherOptions.BatchSize,
            RetryDelay = TimeSpan.FromSeconds(_notificationDispatcherOptions.RetryDelaySeconds),
            ProcessingTimeout = TimeSpan.FromSeconds(_notificationDispatcherOptions.ProcessingTimeoutSeconds),
            CancellationToken = stoppingToken
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

}
