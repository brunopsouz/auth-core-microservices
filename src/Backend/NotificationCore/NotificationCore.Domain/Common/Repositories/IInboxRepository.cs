namespace NotificationCore.Domain.Common.Repositories;

/// <summary>
/// Define operacoes de persistencia para controle idempotente de mensagens consumidas.
/// </summary>
public interface IInboxRepository
{
    /// <summary>
    /// Operacao para tentar iniciar o processamento idempotente da mensagem.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="messageType">Tipo logico da mensagem.</param>
    /// <param name="consumerName">Nome do consumidor.</param>
    /// <param name="payload">Conteudo serializado da mensagem.</param>
    /// <param name="receivedAtUtc">Data de recebimento em UTC.</param>
    /// <returns>Resultado da tentativa de inicio.</returns>
    Task<InboxProcessingStartResult> TryStartProcessingAsync(
        Guid messageId,
        string messageType,
        string consumerName,
        string payload,
        DateTime receivedAtUtc);

    /// <summary>
    /// Operacao para obter o payload original pela chave de idempotencia da notificacao.
    /// </summary>
    /// <param name="idempotencyKey">Chave de idempotencia da notificacao.</param>
    /// <returns>Payload encontrado ou nulo.</returns>
    Task<string?> GetPayloadByNotificationIdempotencyKeyAsync(string idempotencyKey);

    /// <summary>
    /// Operacao para marcar mensagem como processada.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="messageType">Tipo logico da mensagem.</param>
    /// <param name="consumerName">Nome do consumidor.</param>
    /// <param name="processedAtUtc">Data de processamento em UTC.</param>
    Task MarkAsProcessedAsync(
        Guid messageId,
        string messageType,
        string consumerName,
        DateTime processedAtUtc);

    /// <summary>
    /// Operacao para marcar mensagem como falha.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="messageType">Tipo logico da mensagem.</param>
    /// <param name="consumerName">Nome do consumidor.</param>
    /// <param name="payload">Conteudo serializado da mensagem.</param>
    /// <param name="receivedAtUtc">Data de recebimento em UTC.</param>
    /// <param name="error">Erro sanitizado da tentativa.</param>
    Task MarkAsFailedAsync(
        Guid messageId,
        string messageType,
        string consumerName,
        string payload,
        DateTime receivedAtUtc,
        string error);
}

/// <summary>
/// Representa resultado da tentativa de processamento de inbox.
/// </summary>
public sealed class InboxProcessingStartResult
{
    /// <summary>
    /// Indica se esta instancia deve processar a mensagem.
    /// </summary>
    public bool ShouldProcess { get; init; }

    /// <summary>
    /// Indica se a mensagem ja havia sido processada antes.
    /// </summary>
    public bool WasAlreadyProcessed { get; init; }

    /// <summary>
    /// Quantidade atual de tentativas registradas.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Operacao para criar resultado de processamento iniciado.
    /// </summary>
    /// <param name="retryCount">Quantidade atual de tentativas.</param>
    /// <returns>Resultado criado.</returns>
    public static InboxProcessingStartResult Started(int retryCount)
    {
        return new InboxProcessingStartResult
        {
            ShouldProcess = true,
            WasAlreadyProcessed = false,
            RetryCount = retryCount
        };
    }

    /// <summary>
    /// Operacao para criar resultado de mensagem duplicada.
    /// </summary>
    /// <param name="wasAlreadyProcessed">Indica se a mensagem ja estava processada.</param>
    /// <param name="retryCount">Quantidade atual de tentativas.</param>
    /// <returns>Resultado criado.</returns>
    public static InboxProcessingStartResult Skipped(bool wasAlreadyProcessed, int retryCount)
    {
        return new InboxProcessingStartResult
        {
            ShouldProcess = false,
            WasAlreadyProcessed = wasAlreadyProcessed,
            RetryCount = retryCount
        };
    }
}
