using NotificationCore.Domain.Common.Messaging;

namespace NotificationCore.Domain.Common.Repositories;

/// <summary>
/// Define operações de persistência para mensagens de inbox.
/// </summary>
public interface IInboxRepository
{
    /// <summary>
    /// Operação para adicionar uma mensagem de inbox.
    /// </summary>
    /// <param name="message">Mensagem a ser persistida.</param>
    Task AddAsync(InboxMessage message);

    /// <summary>
    /// Operação para tentar adicionar uma mensagem de inbox de forma idempotente.
    /// </summary>
    /// <param name="message">Mensagem a ser persistida.</param>
    /// <returns>Verdadeiro quando a mensagem foi adicionada.</returns>
    Task<bool> TryAddAsync(InboxMessage message);

    /// <summary>
    /// Operação para obter uma mensagem pelo identificador idempotente.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <returns>Mensagem encontrada ou nula.</returns>
    Task<InboxMessage?> GetByMessageIdAsync(Guid messageId);

    /// <summary>
    /// Operação para obter a mensagem original pela chave de idempotência da notificação.
    /// </summary>
    /// <param name="idempotencyKey">Chave de idempotência da notificação.</param>
    /// <returns>Mensagem encontrada ou nula.</returns>
    Task<InboxMessage?> GetByNotificationIdempotencyKeyAsync(string idempotencyKey);

    /// <summary>
    /// Operação para obter mensagens recebidas e ainda não processadas.
    /// </summary>
    /// <param name="take">Quantidade máxima de mensagens.</param>
    /// <returns>Coleção de mensagens pendentes.</returns>
    Task<IReadOnlyCollection<InboxMessage>> GetPendingAsync(int take);

    /// <summary>
    /// Operação para buscar mensagens de inbox por filtros administrativos.
    /// </summary>
    /// <param name="messageId">Identificador idempotente opcional.</param>
    /// <param name="source">Sistema de origem opcional.</param>
    /// <param name="status">Status opcional da mensagem.</param>
    /// <param name="skip">Quantidade de registros ignorados.</param>
    /// <param name="take">Quantidade máxima de registros.</param>
    /// <returns>Coleção de mensagens encontradas.</returns>
    Task<IReadOnlyCollection<InboxMessage>> SearchAsync(
        Guid? messageId,
        string? source,
        InboxMessageStatus? status,
        int skip,
        int take);

    /// <summary>
    /// Operação para verificar se uma mensagem já foi recebida.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <returns>Verdadeiro quando a mensagem já existe.</returns>
    Task<bool> ExistsByMessageIdAsync(Guid messageId);

    /// <summary>
    /// Operação para atualizar uma mensagem de inbox.
    /// </summary>
    /// <param name="message">Mensagem atualizada.</param>
    Task UpdateAsync(InboxMessage message);
}
