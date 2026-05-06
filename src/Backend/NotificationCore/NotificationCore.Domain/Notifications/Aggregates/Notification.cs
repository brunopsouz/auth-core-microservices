using NotificationCore.Domain.Common.Aggregates;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.Entities;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.ValueObjects;

namespace NotificationCore.Domain.Notifications.Aggregates;

/// <summary>
/// Representa uma notificação transacional.
/// </summary>
public sealed class Notification : AggregateRoot
{
    /// <summary>
    /// Quantidade máxima de tentativas antes de finalizar sem entrega.
    /// </summary>
    private const int MAX_DELIVERY_ATTEMPTS = 3;

    private readonly List<DeliveryAttempt> _deliveryAttempts = [];

    /// <summary>
    /// Nome do sistema que originou a notificação.
    /// </summary>
    public string Source { get; private set; } = null!;

    /// <summary>
    /// Identificador de correlação do fluxo de origem.
    /// </summary>
    public string CorrelationId { get; private set; } = null!;

    /// <summary>
    /// Chave usada para garantir idempotência da notificação.
    /// </summary>
    public IdempotencyKey IdempotencyKey { get; private set; } = null!;

    /// <summary>
    /// Canal de entrega da notificação.
    /// </summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>
    /// Destinatário da notificação.
    /// </summary>
    public RecipientEmail Recipient { get; private set; } = null!;

    /// <summary>
    /// Chave do template usado na renderização.
    /// </summary>
    public TemplateKey TemplateKey { get; private set; } = null!;

    /// <summary>
    /// Status atual de processamento.
    /// </summary>
    public NotificationStatus Status { get; private set; }

    /// <summary>
    /// Prioridade de processamento.
    /// </summary>
    public NotificationPriority Priority { get; private set; }

    /// <summary>
    /// Data e hora UTC em que a notificação foi solicitada.
    /// </summary>
    public DateTime RequestedAtUtc { get; private set; }

    /// <summary>
    /// Data e hora UTC em que a notificação pode ser processada.
    /// </summary>
    public DateTime ScheduledAtUtc { get; private set; }

    /// <summary>
    /// Data e hora UTC em que a notificação foi criada.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Data e hora UTC em que a notificação foi enviada.
    /// </summary>
    public DateTime? SentAtUtc { get; private set; }

    /// <summary>
    /// Data e hora UTC em que a notificação falhou definitivamente.
    /// </summary>
    public DateTime? FailedAtUtc { get; private set; }

    /// <summary>
    /// Último erro registrado para a notificação.
    /// </summary>
    public string LastError { get; private set; } = string.Empty;

    /// <summary>
    /// Tentativas de entrega vinculadas à notificação.
    /// </summary>
    public IReadOnlyCollection<DeliveryAttempt> DeliveryAttempts => _deliveryAttempts.AsReadOnly();

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private Notification()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="source">Sistema de origem.</param>
    /// <param name="correlationId">Identificador de correlação.</param>
    /// <param name="idempotencyKey">Chave de idempotência.</param>
    /// <param name="channel">Canal de entrega.</param>
    /// <param name="recipient">Destinatário.</param>
    /// <param name="templateKey">Chave do template.</param>
    /// <param name="status">Status de processamento.</param>
    /// <param name="priority">Prioridade de processamento.</param>
    /// <param name="requestedAtUtc">Data da solicitação.</param>
    /// <param name="scheduledAtUtc">Data de agendamento.</param>
    /// <param name="createdAtUtc">Data de criação.</param>
    /// <param name="sentAtUtc">Data de envio.</param>
    /// <param name="failedAtUtc">Data de falha definitiva.</param>
    /// <param name="lastError">Último erro registrado.</param>
    private Notification(
        string source,
        string correlationId,
        IdempotencyKey idempotencyKey,
        NotificationChannel channel,
        RecipientEmail recipient,
        TemplateKey templateKey,
        NotificationStatus status,
        NotificationPriority priority,
        DateTime requestedAtUtc,
        DateTime scheduledAtUtc,
        DateTime createdAtUtc,
        DateTime? sentAtUtc,
        DateTime? failedAtUtc,
        string? lastError)
    {
        Source = Normalize(source);
        CorrelationId = Normalize(correlationId);
        IdempotencyKey = idempotencyKey;
        Channel = channel;
        Recipient = recipient;
        TemplateKey = templateKey;
        Status = status;
        Priority = priority;
        RequestedAtUtc = requestedAtUtc;
        ScheduledAtUtc = scheduledAtUtc;
        CreatedAtUtc = createdAtUtc;
        SentAtUtc = sentAtUtc;
        FailedAtUtc = failedAtUtc;
        LastError = Normalize(lastError);

        Validate();
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador da notificação.</param>
    /// <param name="source">Sistema de origem.</param>
    /// <param name="correlationId">Identificador de correlação.</param>
    /// <param name="idempotencyKey">Chave de idempotência.</param>
    /// <param name="channel">Canal de entrega.</param>
    /// <param name="recipient">Destinatário.</param>
    /// <param name="templateKey">Chave do template.</param>
    /// <param name="status">Status de processamento.</param>
    /// <param name="priority">Prioridade de processamento.</param>
    /// <param name="requestedAtUtc">Data da solicitação.</param>
    /// <param name="scheduledAtUtc">Data de agendamento.</param>
    /// <param name="createdAtUtc">Data de criação.</param>
    /// <param name="deliveryAttempts">Tentativas de entrega persistidas.</param>
    /// <param name="sentAtUtc">Data de envio.</param>
    /// <param name="failedAtUtc">Data de falha definitiva.</param>
    /// <param name="lastError">Último erro registrado.</param>
    private Notification(
        Guid id,
        string source,
        string correlationId,
        IdempotencyKey idempotencyKey,
        NotificationChannel channel,
        RecipientEmail recipient,
        TemplateKey templateKey,
        NotificationStatus status,
        NotificationPriority priority,
        DateTime requestedAtUtc,
        DateTime scheduledAtUtc,
        DateTime createdAtUtc,
        IEnumerable<DeliveryAttempt>? deliveryAttempts,
        DateTime? sentAtUtc,
        DateTime? failedAtUtc,
        string? lastError)
        : base(id)
    {
        Source = Normalize(source);
        CorrelationId = Normalize(correlationId);
        IdempotencyKey = idempotencyKey;
        Channel = channel;
        Recipient = recipient;
        TemplateKey = templateKey;
        Status = status;
        Priority = priority;
        RequestedAtUtc = requestedAtUtc;
        ScheduledAtUtc = scheduledAtUtc;
        CreatedAtUtc = createdAtUtc;
        SentAtUtc = sentAtUtc;
        FailedAtUtc = failedAtUtc;
        LastError = Normalize(lastError);

        Validate();
        RestoreDeliveryAttempts(deliveryAttempts);
        ValidateDeliveryAttemptsState();
    }

    #endregion

    #region Factory

    /// <summary>
    /// Operação para criar notificação pendente.
    /// </summary>
    /// <param name="source">Sistema de origem.</param>
    /// <param name="correlationId">Identificador de correlação.</param>
    /// <param name="recipient">E-mail do destinatário.</param>
    /// <param name="templateKey">Chave do template.</param>
    /// <param name="idempotencyKey">Chave de idempotência.</param>
    /// <param name="channel">Canal de entrega.</param>
    /// <param name="priority">Prioridade de processamento.</param>
    /// <param name="requestedAtUtc">Data da solicitação.</param>
    /// <returns>Instância criada de <see cref="Notification"/>.</returns>
    public static Notification Create(
        string source,
        string correlationId,
        string recipient,
        string templateKey,
        string idempotencyKey,
        NotificationChannel channel,
        NotificationPriority priority,
        DateTime requestedAtUtc)
    {
        return new Notification(
            source,
            correlationId,
            IdempotencyKey.Create(idempotencyKey),
            channel,
            RecipientEmail.Create(recipient),
            TemplateKey.Create(templateKey),
            NotificationStatus.Pending,
            priority,
            requestedAtUtc,
            scheduledAtUtc: requestedAtUtc,
            createdAtUtc: DateTime.UtcNow,
            sentAtUtc: null,
            failedAtUtc: null,
            lastError: null);
    }

    /// <summary>
    /// Operação para restaurar notificação persistida.
    /// </summary>
    /// <param name="id">Identificador da notificação.</param>
    /// <param name="source">Sistema de origem.</param>
    /// <param name="correlationId">Identificador de correlação.</param>
    /// <param name="idempotencyKey">Chave de idempotência.</param>
    /// <param name="channel">Canal de entrega.</param>
    /// <param name="recipient">E-mail do destinatário.</param>
    /// <param name="templateKey">Chave do template.</param>
    /// <param name="status">Status de processamento.</param>
    /// <param name="priority">Prioridade de processamento.</param>
    /// <param name="requestedAtUtc">Data da solicitação.</param>
    /// <param name="scheduledAtUtc">Data de agendamento.</param>
    /// <param name="createdAtUtc">Data de criação.</param>
    /// <param name="deliveryAttempts">Tentativas de entrega persistidas.</param>
    /// <param name="sentAtUtc">Data de envio.</param>
    /// <param name="failedAtUtc">Data de falha definitiva.</param>
    /// <param name="lastError">Último erro registrado.</param>
    /// <returns>Instância restaurada de <see cref="Notification"/>.</returns>
    public static Notification Restore(
        Guid id,
        string source,
        string correlationId,
        string idempotencyKey,
        NotificationChannel channel,
        string recipient,
        string templateKey,
        NotificationStatus status,
        NotificationPriority priority,
        DateTime requestedAtUtc,
        DateTime scheduledAtUtc,
        DateTime createdAtUtc,
        IEnumerable<DeliveryAttempt>? deliveryAttempts = null,
        DateTime? sentAtUtc = null,
        DateTime? failedAtUtc = null,
        string? lastError = null)
    {
        return new Notification(
            id,
            source,
            correlationId,
            IdempotencyKey.Create(idempotencyKey),
            channel,
            RecipientEmail.Create(recipient),
            TemplateKey.Create(templateKey),
            status,
            priority,
            requestedAtUtc,
            scheduledAtUtc,
            createdAtUtc,
            deliveryAttempts,
            sentAtUtc,
            failedAtUtc,
            lastError);
    }

    #endregion

    /// <summary>
    /// Operação para iniciar processamento da notificação.
    /// </summary>
    /// <param name="startedAtUtc">Data de início do processamento.</param>
    public void StartProcessing(DateTime startedAtUtc)
    {
        ValidateUtc(startedAtUtc, "A data de início do processamento é obrigatória e deve estar em UTC.");
        EnsureStatusIs(
            [NotificationStatus.Pending, NotificationStatus.RetryScheduled],
            "A notificação só pode iniciar processamento quando estiver pendente ou com retry agendado.");
        DomainException.When(startedAtUtc < ScheduledAtUtc, "A notificação só pode iniciar processamento na data agendada.");

        Status = NotificationStatus.Processing;
    }

    /// <summary>
    /// Operação para registrar tentativa bem-sucedida.
    /// </summary>
    /// <param name="provider">Provedor usado na tentativa.</param>
    /// <param name="startedAtUtc">Data de início da tentativa.</param>
    /// <param name="finishedAtUtc">Data de conclusão da tentativa.</param>
    /// <param name="providerMessageId">Identificador retornado pelo provedor.</param>
    /// <returns>Tentativa registrada.</returns>
    public DeliveryAttempt RegisterSuccessfulDeliveryAttempt(
        string provider,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        string? providerMessageId)
    {
        EnsureStatusIs(
            [NotificationStatus.Processing],
            "A tentativa bem-sucedida só pode ser registrada em notificação em processamento.");

        var attemptNumber = GetNextAttemptNumber();
        EnsureCanRegisterAttempt(attemptNumber);

        var attempt = DeliveryAttempt.RegisterSuccess(
            Id,
            provider,
            attemptNumber,
            startedAtUtc,
            finishedAtUtc,
            providerMessageId);

        _deliveryAttempts.Add(attempt);
        Status = NotificationStatus.Sent;
        SentAtUtc = finishedAtUtc;
        FailedAtUtc = null;
        LastError = string.Empty;

        return attempt;
    }

    /// <summary>
    /// Operação para registrar tentativa com falha temporária.
    /// </summary>
    /// <param name="provider">Provedor usado na tentativa.</param>
    /// <param name="startedAtUtc">Data de início da tentativa.</param>
    /// <param name="finishedAtUtc">Data de conclusão da tentativa.</param>
    /// <param name="retryAtUtc">Data para nova tentativa.</param>
    /// <param name="errorCode">Código de erro.</param>
    /// <param name="errorMessage">Mensagem de erro.</param>
    /// <returns>Tentativa registrada.</returns>
    public DeliveryAttempt RegisterTemporaryFailureDeliveryAttempt(
        string provider,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        DateTime? retryAtUtc,
        string? errorCode,
        string errorMessage)
    {
        EnsureStatusIs(
            [NotificationStatus.Processing],
            "A falha temporária só pode ser registrada em notificação em processamento.");

        var attemptNumber = GetNextAttemptNumber();
        EnsureCanRegisterAttempt(attemptNumber);

        if (attemptNumber < MAX_DELIVERY_ATTEMPTS)
        {
            DomainException.When(!retryAtUtc.HasValue, "A data de retry é obrigatória quando ainda existem tentativas disponíveis.");
            ValidateUtc(retryAtUtc!.Value, "A data de retry é obrigatória e deve estar em UTC.");
            DomainException.When(retryAtUtc.Value <= finishedAtUtc, "A data de retry deve ser posterior à conclusão da tentativa.");
        }

        var attempt = DeliveryAttempt.RegisterFailure(
            Id,
            provider,
            attemptNumber,
            startedAtUtc,
            finishedAtUtc,
            errorCode,
            errorMessage);

        _deliveryAttempts.Add(attempt);
        LastError = Normalize(errorMessage);

        if (attempt.AttemptNumber >= MAX_DELIVERY_ATTEMPTS)
        {
            Status = NotificationStatus.DeadLettered;
            FailedAtUtc = finishedAtUtc;
            return attempt;
        }

        Status = NotificationStatus.RetryScheduled;
        ScheduledAtUtc = retryAtUtc!.Value;

        return attempt;
    }

    /// <summary>
    /// Operação para registrar tentativa com falha permanente.
    /// </summary>
    /// <param name="provider">Provedor usado na tentativa.</param>
    /// <param name="startedAtUtc">Data de início da tentativa.</param>
    /// <param name="finishedAtUtc">Data de conclusão da tentativa.</param>
    /// <param name="errorCode">Código de erro.</param>
    /// <param name="errorMessage">Mensagem de erro.</param>
    /// <returns>Tentativa registrada.</returns>
    public DeliveryAttempt RegisterPermanentFailureDeliveryAttempt(
        string provider,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        string? errorCode,
        string errorMessage)
    {
        EnsureStatusIs(
            [NotificationStatus.Processing],
            "A falha permanente só pode ser registrada em notificação em processamento.");

        var attemptNumber = GetNextAttemptNumber();
        EnsureCanRegisterAttempt(attemptNumber);

        var attempt = DeliveryAttempt.RegisterFailure(
            Id,
            provider,
            attemptNumber,
            startedAtUtc,
            finishedAtUtc,
            errorCode,
            errorMessage);

        _deliveryAttempts.Add(attempt);
        Status = NotificationStatus.DeadLettered;
        FailedAtUtc = finishedAtUtc;
        LastError = Normalize(errorMessage);

        return attempt;
    }

    /// <summary>
    /// Operação para marcar notificação como finalizada sem entrega.
    /// </summary>
    /// <param name="failedAtUtc">Data de falha definitiva.</param>
    /// <param name="lastError">Último erro registrado.</param>
    public void MarkAsDeadLettered(
        DateTime failedAtUtc,
        string lastError)
    {
        ValidateUtc(failedAtUtc, "A data de falha definitiva é obrigatória e deve estar em UTC.");
        EnsureStatusIs(
            [NotificationStatus.Processing, NotificationStatus.RetryScheduled],
            "A notificação só pode ser marcada como dead letter quando estiver em processamento ou com retry agendado.");
        DomainException.When(string.IsNullOrWhiteSpace(lastError), "O último erro é obrigatório para finalizar a notificação sem entrega.");

        Status = NotificationStatus.DeadLettered;
        FailedAtUtc = failedAtUtc;
        LastError = Normalize(lastError);
    }

    #region Helpers

    /// <summary>
    /// Operação para validar os dados da notificação.
    /// </summary>
    private void Validate()
    {
        DomainException.When(Id == Guid.Empty, "O identificador da notificação é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(Source), "A origem da notificação é obrigatória.");
        DomainException.When(string.IsNullOrWhiteSpace(CorrelationId), "O identificador de correlação é obrigatório.");
        DomainException.When(IdempotencyKey is null, "A chave de idempotência é obrigatória.");
        DomainException.When(Recipient is null, "O destinatário da notificação é obrigatório.");
        DomainException.When(TemplateKey is null, "A chave do template é obrigatória.");
        DomainException.When(!Enum.IsDefined(typeof(NotificationChannel), Channel), "Canal de notificação inválido.");
        DomainException.When(!Enum.IsDefined(typeof(NotificationStatus), Status), "Status de notificação inválido.");
        DomainException.When(!Enum.IsDefined(typeof(NotificationPriority), Priority), "Prioridade de notificação inválida.");
        ValidateUtc(RequestedAtUtc, "A data de solicitação da notificação é obrigatória e deve estar em UTC.");
        ValidateUtc(ScheduledAtUtc, "A data de agendamento da notificação é obrigatória e deve estar em UTC.");
        ValidateUtc(CreatedAtUtc, "A data de criação da notificação é obrigatória e deve estar em UTC.");
        ValidateNullableUtc(SentAtUtc, "A data de envio deve estar em UTC.");
        ValidateNullableUtc(FailedAtUtc, "A data de falha definitiva deve estar em UTC.");
        DomainException.When(ScheduledAtUtc < RequestedAtUtc, "A data de agendamento não pode ser anterior à solicitação.");
        DomainException.When(Status == NotificationStatus.Sent && !SentAtUtc.HasValue, "A notificação enviada deve possuir data de envio.");
        DomainException.When(Status != NotificationStatus.Sent && SentAtUtc.HasValue, "A data de envio só pode existir em notificação enviada.");
        DomainException.When(Status == NotificationStatus.DeadLettered && !FailedAtUtc.HasValue, "A notificação finalizada sem entrega deve possuir data de falha.");
        DomainException.When(Status != NotificationStatus.DeadLettered && FailedAtUtc.HasValue, "A data de falha definitiva só pode existir em notificação dead letter.");
        DomainException.When(SentAtUtc.HasValue && SentAtUtc.Value < RequestedAtUtc, "A data de envio não pode ser anterior à solicitação.");
        DomainException.When(FailedAtUtc.HasValue && FailedAtUtc.Value < RequestedAtUtc, "A data de falha definitiva não pode ser anterior à solicitação.");
        DomainException.When(Status == NotificationStatus.Sent && !string.IsNullOrWhiteSpace(LastError), "A notificação enviada não deve possuir último erro.");
        DomainException.When(Status == NotificationStatus.RetryScheduled && string.IsNullOrWhiteSpace(LastError), "A notificação com retry agendado deve possuir último erro.");
        DomainException.When(Status == NotificationStatus.DeadLettered && string.IsNullOrWhiteSpace(LastError), "A notificação dead letter deve possuir último erro.");
    }

    /// <summary>
    /// Operação para restaurar tentativas de entrega.
    /// </summary>
    /// <param name="deliveryAttempts">Tentativas de entrega persistidas.</param>
    private void RestoreDeliveryAttempts(IEnumerable<DeliveryAttempt>? deliveryAttempts)
    {
        if (deliveryAttempts is null)
            return;

        var attempts = deliveryAttempts.ToList();

        DomainException.When(attempts.Any(deliveryAttempt => deliveryAttempt is null), "A tentativa de entrega restaurada é obrigatória.");
        DomainException.When(attempts.Select(deliveryAttempt => deliveryAttempt.AttemptNumber).Distinct().Count() != attempts.Count, "Não é permitido restaurar tentativas com número duplicado.");

        attempts = attempts
            .OrderBy(deliveryAttempt => deliveryAttempt.AttemptNumber)
            .ToList();

        for (var index = 0; index < attempts.Count; index++)
        {
            DomainException.When(attempts[index].AttemptNumber != index + 1, "As tentativas restauradas devem possuir numeração sequencial.");
        }

        foreach (var deliveryAttempt in attempts)
        {
            DomainException.When(deliveryAttempt.NotificationId != Id, "A tentativa deve pertencer à notificação restaurada.");
            _deliveryAttempts.Add(deliveryAttempt);
        }
    }

    /// <summary>
    /// Operação para validar o estado das tentativas restauradas.
    /// </summary>
    private void ValidateDeliveryAttemptsState()
    {
        DomainException.When(_deliveryAttempts.Count > MAX_DELIVERY_ATTEMPTS, "A quantidade de tentativas excede o limite permitido.");
        DomainException.When(_deliveryAttempts.Count >= MAX_DELIVERY_ATTEMPTS && Status is not NotificationStatus.Sent and not NotificationStatus.DeadLettered, "Notificação com limite de tentativas atingido deve estar em estado final.");
    }

    /// <summary>
    /// Operação para obter o próximo número de tentativa.
    /// </summary>
    /// <returns>Número da próxima tentativa.</returns>
    private int GetNextAttemptNumber()
    {
        if (_deliveryAttempts.Count == 0)
            return 1;

        return _deliveryAttempts.Max(deliveryAttempt => deliveryAttempt.AttemptNumber) + 1;
    }

    /// <summary>
    /// Operação para garantir que nova tentativa pode ser registrada.
    /// </summary>
    /// <param name="attemptNumber">Número da tentativa a registrar.</param>
    private static void EnsureCanRegisterAttempt(int attemptNumber)
    {
        DomainException.When(attemptNumber > MAX_DELIVERY_ATTEMPTS, "O limite de tentativas da notificação foi atingido.");
    }

    /// <summary>
    /// Operação para garantir que o status atual permite a operação.
    /// </summary>
    /// <param name="allowedStatuses">Status permitidos.</param>
    /// <param name="message">Mensagem usada quando a transição é inválida.</param>
    private void EnsureStatusIs(
        IReadOnlyCollection<NotificationStatus> allowedStatuses,
        string message)
    {
        DomainException.When(!allowedStatuses.Contains(Status), message);
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

        DomainException.When(value.Value == default || value.Value.Kind != DateTimeKind.Utc, message);
    }

    #endregion
}
