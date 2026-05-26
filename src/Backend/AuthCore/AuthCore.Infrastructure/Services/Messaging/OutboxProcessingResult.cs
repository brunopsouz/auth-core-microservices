namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa o resultado de um ciclo de processamento da outbox.
/// </summary>
internal sealed class OutboxProcessingResult
{
    /// <summary>
    /// Quantidade de mensagens processadas com sucesso.
    /// </summary>
    public int ProcessedCount { get; init; }

    /// <summary>
    /// Quantidade de mensagens registradas com falha.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Quantidade total de mensagens avaliadas no ciclo.
    /// </summary>
    public int TotalCount => ProcessedCount + FailedCount;
}
