namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Define operações de persistência para mensagens de outbox.
/// </summary>
internal interface IOutboxMessageRepository
{
    /// <summary>
    /// Operação para adicionar uma mensagem de outbox.
    /// </summary>
    /// <param name="message">Mensagem a ser persistida.</param>
    Task AddAsync(OutboxMessage message);

    /// <summary>
    /// Operação para obter mensagens pendentes de processamento.
    /// </summary>
    /// <param name="take">Quantidade máxima de mensagens.</param>
    /// <param name="maxAttempts">Quantidade máxima de tentativas permitidas.</param>
    /// <returns>Coleção de mensagens pendentes.</returns>
    Task<IReadOnlyCollection<OutboxMessage>> GetPendingAsync(int take, int maxAttempts);

    /// <summary>
    /// Operação para atualizar uma mensagem de outbox.
    /// </summary>
    /// <param name="message">Mensagem atualizada.</param>
    Task UpdateAsync(OutboxMessage message);
}
