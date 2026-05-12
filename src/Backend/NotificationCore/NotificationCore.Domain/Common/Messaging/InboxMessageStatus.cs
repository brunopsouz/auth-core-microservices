namespace NotificationCore.Domain.Common.Messaging;

/// <summary>
/// Representa os estados possíveis de uma mensagem de inbox.
/// </summary>
public enum InboxMessageStatus
{
    /// <summary>
    /// Mensagem recebida e ainda não processada.
    /// </summary>
    Received = 1,

    /// <summary>
    /// Mensagem processada com sucesso.
    /// </summary>
    Processed = 2,

    /// <summary>
    /// Mensagem processada com falha.
    /// </summary>
    Failed = 3
}
