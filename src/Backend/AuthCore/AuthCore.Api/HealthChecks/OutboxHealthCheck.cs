using AuthCore.Domain.Common.Repositories;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.HealthChecks;

/// <summary>
/// Representa health check para a consulta da outbox.
/// </summary>
internal sealed class OutboxHealthCheck : IHealthCheck
{
    /// <summary>
    /// Campo que armazena outbox repository.
    /// </summary>
    private readonly IOutboxRepository _outboxRepository;
    /// <summary>
    /// Campo que armazena outbox options.
    /// </summary>
    private readonly OutboxOptions _outboxOptions;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="outboxRepository">Repositório da outbox.</param>
    /// <param name="outboxOptions">Opções de processamento da outbox.</param>
    public OutboxHealthCheck(
        IOutboxRepository outboxRepository,
        IOptions<OutboxOptions> outboxOptions)
    {
        _outboxRepository = outboxRepository;
        _outboxOptions = outboxOptions.Value;
    }

    /// <summary>
    /// Operação para verificar a saúde da consulta de outbox.
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
            _ = await _outboxRepository.GetPendingAsync(take: 1, maxAttempts: _outboxOptions.MaxAttempts);

            return HealthCheckResult.Healthy("Outbox consultável.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao consultar a outbox.", exception);
        }
    }
}
