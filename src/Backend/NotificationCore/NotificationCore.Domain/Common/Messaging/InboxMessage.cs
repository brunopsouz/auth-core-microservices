using NotificationCore.Domain.Common.Exceptions;

namespace NotificationCore.Domain.Common.Messaging;

/// <summary>
/// Representa uma mensagem persistida na inbox.
/// </summary>
public sealed class InboxMessage
{
    /// <summary>
    /// Identificador idempotente da mensagem recebida.
    /// </summary>
    public Guid MessageId { get; private set; }

    /// <summary>
    /// Nome do sistema que originou a mensagem.
    /// </summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>
    /// Tipo lógico da mensagem.
    /// </summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>
    /// Conteúdo serializado da mensagem.
    /// </summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>
    /// Data de recebimento em UTC.
    /// </summary>
    public DateTime ReceivedAtUtc { get; private set; }

    /// <summary>
    /// Data de processamento em UTC.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>
    /// Status de processamento da mensagem.
    /// </summary>
    public InboxMessageStatus Status { get; private set; }

    /// <summary>
    /// Erro registrado no processamento da mensagem.
    /// </summary>
    public string Error { get; private set; } = string.Empty;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private InboxMessage()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="source">Sistema de origem.</param>
    /// <param name="type">Tipo lógico da mensagem.</param>
    /// <param name="payload">Conteúdo serializado.</param>
    /// <param name="receivedAtUtc">Data de recebimento.</param>
    /// <param name="processedAtUtc">Data de processamento.</param>
    /// <param name="status">Status de processamento.</param>
    /// <param name="error">Erro de processamento.</param>
    private InboxMessage(
        Guid messageId,
        string source,
        string type,
        string payload,
        DateTime receivedAtUtc,
        DateTime? processedAtUtc,
        InboxMessageStatus status,
        string? error)
    {
        MessageId = messageId;
        Source = Normalize(source);
        Type = Normalize(type);
        Payload = Normalize(payload);
        ReceivedAtUtc = receivedAtUtc;
        ProcessedAtUtc = processedAtUtc;
        Status = status;
        Error = Normalize(error);

        Validate();
    }

    #endregion

    #region Factory

    /// <summary>
    /// Operação para criar mensagem de inbox recebida.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="source">Sistema de origem.</param>
    /// <param name="type">Tipo lógico da mensagem.</param>
    /// <param name="payload">Conteúdo serializado.</param>
    /// <param name="receivedAtUtc">Data de recebimento.</param>
    /// <returns>Mensagem criada.</returns>
    public static InboxMessage Create(
        Guid messageId,
        string source,
        string type,
        string payload,
        DateTime receivedAtUtc)
    {
        return new InboxMessage(
            messageId,
            source,
            type,
            payload,
            receivedAtUtc,
            processedAtUtc: null,
            InboxMessageStatus.Received,
            error: null);
    }

    /// <summary>
    /// Operação para restaurar mensagem de inbox persistida.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="source">Sistema de origem.</param>
    /// <param name="type">Tipo lógico da mensagem.</param>
    /// <param name="payload">Conteúdo serializado.</param>
    /// <param name="receivedAtUtc">Data de recebimento.</param>
    /// <param name="processedAtUtc">Data de processamento.</param>
    /// <param name="status">Status de processamento.</param>
    /// <param name="error">Erro de processamento.</param>
    /// <returns>Mensagem restaurada.</returns>
    public static InboxMessage Restore(
        Guid messageId,
        string source,
        string type,
        string payload,
        DateTime receivedAtUtc,
        DateTime? processedAtUtc,
        InboxMessageStatus status,
        string? error)
    {
        return new InboxMessage(
            messageId,
            source,
            type,
            payload,
            receivedAtUtc,
            processedAtUtc,
            status,
            error);
    }

    #endregion

    /// <summary>
    /// Operação para marcar a mensagem como processada.
    /// </summary>
    /// <param name="processedAtUtc">Data de processamento.</param>
    public void MarkAsProcessed(DateTime processedAtUtc)
    {
        EnsureStatusIs(InboxMessageStatus.Received, "Apenas mensagem recebida pode ser marcada como processada.");
        ValidateUtc(processedAtUtc, "A data de processamento da inbox é obrigatória e deve estar em UTC.");
        DomainException.When(processedAtUtc < ReceivedAtUtc, "A data de processamento não pode ser anterior ao recebimento.");

        Status = InboxMessageStatus.Processed;
        ProcessedAtUtc = processedAtUtc;
        Error = string.Empty;

        Validate();
    }

    /// <summary>
    /// Operação para registrar falha de processamento.
    /// </summary>
    /// <param name="error">Erro de processamento.</param>
    public void MarkAsFailed(string error)
    {
        EnsureStatusIs(InboxMessageStatus.Received, "Apenas mensagem recebida pode ser marcada com falha.");
        DomainException.When(string.IsNullOrWhiteSpace(error), "O erro de processamento da inbox é obrigatório.");

        Status = InboxMessageStatus.Failed;
        ProcessedAtUtc = null;
        Error = Normalize(error);

        Validate();
    }

    #region Helpers

    /// <summary>
    /// Operação para validar a consistência da mensagem.
    /// </summary>
    private void Validate()
    {
        DomainException.When(MessageId == Guid.Empty, "O identificador da mensagem de inbox é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(Source), "A origem da mensagem de inbox é obrigatória.");
        DomainException.When(string.IsNullOrWhiteSpace(Type), "O tipo da mensagem de inbox é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(Payload), "O conteúdo da mensagem de inbox é obrigatório.");
        DomainException.When(!Enum.IsDefined(typeof(InboxMessageStatus), Status), "Status de inbox inválido.");
        ValidateUtc(ReceivedAtUtc, "A data de recebimento da inbox é obrigatória e deve estar em UTC.");
        ValidateNullableUtc(ProcessedAtUtc, "A data de processamento da inbox deve estar em UTC.");
        DomainException.When(ProcessedAtUtc.HasValue && ProcessedAtUtc.Value < ReceivedAtUtc, "A data de processamento não pode ser anterior ao recebimento.");
        DomainException.When(Status == InboxMessageStatus.Processed && !ProcessedAtUtc.HasValue, "A mensagem processada deve possuir data de processamento.");
        DomainException.When(Status != InboxMessageStatus.Processed && ProcessedAtUtc.HasValue, "A data de processamento só pode existir em mensagem processada.");
        DomainException.When(Status == InboxMessageStatus.Failed && string.IsNullOrWhiteSpace(Error), "A mensagem com falha deve possuir erro.");
        DomainException.When(Status != InboxMessageStatus.Failed && !string.IsNullOrWhiteSpace(Error), "O erro só pode existir em mensagem com falha.");
    }

    /// <summary>
    /// Operação para garantir que o status atual permite a operação.
    /// </summary>
    /// <param name="status">Status permitido.</param>
    /// <param name="message">Mensagem usada quando a transição é inválida.</param>
    private void EnsureStatusIs(InboxMessageStatus status, string message)
    {
        DomainException.When(Status != status, message);
    }

    /// <summary>
    /// Operação para normalizar texto opcional.
    /// </summary>
    /// <param name="value">Valor a normalizar.</param>
    /// <returns>Valor normalizado.</returns>
    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Operação para validar data UTC.
    /// </summary>
    /// <param name="value">Data a validar.</param>
    /// <param name="message">Mensagem usada quando a data é inválida.</param>
    private static void ValidateUtc(DateTime value, string message)
    {
        DomainException.When(value == default || value.Kind != DateTimeKind.Utc, message);
    }

    /// <summary>
    /// Operação para validar data UTC opcional.
    /// </summary>
    /// <param name="value">Data a validar.</param>
    /// <param name="message">Mensagem usada quando a data é inválida.</param>
    private static void ValidateNullableUtc(DateTime? value, string message)
    {
        if (!value.HasValue)
            return;

        ValidateUtc(value.Value, message);
    }

    #endregion
}
