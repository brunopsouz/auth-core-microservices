namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Define operação para processar mensagens pendentes da outbox.
/// </summary>
internal interface IOutboxProcessor
{
    /// <summary>
    /// Operação para processar mensagens pendentes da outbox.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Resultado do ciclo de processamento.</returns>
    Task<OutboxProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
