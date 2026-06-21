namespace AuthCore.Domain.Common.Repositories;

/// <summary>
/// Define operações de persistência para mensagens de outbox.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Operação para adicionar uma mensagem de outbox.
    /// </summary>
    /// <param name="message">Mensagem a ser persistida.</param>
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Operação para obter mensagens pendentes de processamento.
    /// </summary>
    /// <param name="take">Quantidade máxima de mensagens.</param>
    /// <param name="maxAttempts">Quantidade máxima de tentativas permitidas.</param>
    /// <returns>Coleção de mensagens pendentes.</returns>
    Task<IReadOnlyCollection<OutboxMessage>> GetPendingAsync(
        int take,
        int maxAttempts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Operação para reservar a próxima mensagem pendente.
    /// </summary>
    /// <param name="leaseId">Identificador exclusivo do lease.</param>
    /// <param name="leasedUntilUtc">Data de expiração do lease em UTC.</param>
    /// <param name="maxAttempts">Quantidade máxima de tentativas permitidas.</param>
    /// <param name="nowUtc">Data atual usada para recuperar leases expirados.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Mensagem reservada ou nula quando não houver item disponível.</returns>
    Task<OutboxMessage?> ClaimPendingAsync(
        Guid leaseId,
        DateTime leasedUntilUtc,
        int maxAttempts,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Operação para concluir uma mensagem reservada.
    /// </summary>
    Task<bool> MarkAsProcessedAsync(
        Guid messageId,
        Guid leaseId,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Operação para registrar falha de uma mensagem reservada.
    /// </summary>
    Task<bool> RegisterFailureAsync(
        Guid messageId,
        Guid leaseId,
        string errorMessage,
        CancellationToken cancellationToken = default);
}
