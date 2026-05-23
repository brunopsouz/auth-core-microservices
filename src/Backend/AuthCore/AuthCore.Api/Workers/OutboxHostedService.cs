using AuthCore.Infrastructure.Configurations;
using AuthCore.Infrastructure.Services.Messaging;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Workers;

/// <summary>
/// Representa worker hospedado para processamento da outbox.
/// </summary>
internal sealed class OutboxHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly OutboxOptions _outboxOptions;
    private readonly ILogger<OutboxHostedService> _logger;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="serviceScopeFactory">Fábrica de escopos da aplicação.</param>
    /// <param name="outboxOptions">Opções de processamento da outbox.</param>
    /// <param name="logger">Serviço de logging.</param>
    public OutboxHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<OutboxOptions> outboxOptions,
        ILogger<OutboxHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _outboxOptions = outboxOptions.Value;
        _logger = logger;
    }

    #endregion

    /// <summary>
    /// Operação para executar o worker da outbox.
    /// </summary>
    /// <param name="stoppingToken">Token para cancelamento da execução.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_outboxOptions.Enabled)
        {
            _logger.LogInformation("Worker da outbox desabilitado por configuração.");
            return;
        }

        _logger.LogInformation("Worker da outbox iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

                await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Falha no ciclo do worker da outbox.");
            }

            await DelayUntilNextCycleAsync(stoppingToken);
        }

        _logger.LogInformation("Worker da outbox encerrado.");
    }

    #region Helpers

    /// <summary>
    /// Operação para aguardar o próximo ciclo de processamento.
    /// </summary>
    /// <param name="stoppingToken">Token para cancelamento da espera.</param>
    private Task DelayUntilNextCycleAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(_outboxOptions.PollingIntervalSeconds);

        return Task.Delay(delay, stoppingToken);
    }

    #endregion
}
