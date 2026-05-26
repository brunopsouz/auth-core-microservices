using NotificationCore.Domain.Common.Entities;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Domain.Notifications.Entities;

/// <summary>
/// Representa uma tentativa de entrega da notificação.
/// </summary>
public sealed class DeliveryAttempt : EntityBase
{
    /// <summary>
    /// Identificador da notificação vinculada.
    /// </summary>
    public Guid NotificationId { get; private set; }

    /// <summary>
    /// Provedor usado na tentativa de entrega.
    /// </summary>
    public string Provider { get; private set; } = null!;

    /// <summary>
    /// Status da tentativa de entrega.
    /// </summary>
    public DeliveryAttemptStatus Status { get; private set; }

    /// <summary>
    /// Número sequencial da tentativa.
    /// </summary>
    public int AttemptNumber { get; private set; }

    /// <summary>
    /// Data e hora UTC de início da tentativa.
    /// </summary>
    public DateTime StartedAtUtc { get; private set; }

    /// <summary>
    /// Data e hora UTC de conclusão da tentativa.
    /// </summary>
    public DateTime FinishedAtUtc { get; private set; }

    /// <summary>
    /// Código de erro retornado pelo provedor.
    /// </summary>
    public string ErrorCode { get; private set; } = null!;

    /// <summary>
    /// Mensagem de erro retornada pelo provedor.
    /// </summary>
    public string ErrorMessage { get; private set; } = null!;

    /// <summary>
    /// Identificador da mensagem retornado pelo provedor.
    /// </summary>
    public string ProviderMessageId { get; private set; } = null!;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private DeliveryAttempt()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="notificationId">Identificador da notificação.</param>
    /// <param name="provider">Provedor usado na tentativa.</param>
    /// <param name="status">Status da tentativa.</param>
    /// <param name="attemptNumber">Número sequencial da tentativa.</param>
    /// <param name="startedAtUtc">Data de início.</param>
    /// <param name="finishedAtUtc">Data de conclusão.</param>
    /// <param name="errorCode">Código de erro.</param>
    /// <param name="errorMessage">Mensagem de erro.</param>
    /// <param name="providerMessageId">Identificador retornado pelo provedor.</param>
    private DeliveryAttempt(
        Guid notificationId,
        string provider,
        DeliveryAttemptStatus status,
        int attemptNumber,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        string? errorCode,
        string? errorMessage,
        string? providerMessageId)
    {
        NotificationId = notificationId;
        Provider = Normalize(provider);
        Status = status;
        AttemptNumber = attemptNumber;
        StartedAtUtc = startedAtUtc;
        FinishedAtUtc = finishedAtUtc;
        ErrorCode = Normalize(errorCode);
        ErrorMessage = Normalize(errorMessage);
        ProviderMessageId = Normalize(providerMessageId);

        Validate();
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador da tentativa.</param>
    /// <param name="notificationId">Identificador da notificação.</param>
    /// <param name="provider">Provedor usado na tentativa.</param>
    /// <param name="status">Status da tentativa.</param>
    /// <param name="attemptNumber">Número sequencial da tentativa.</param>
    /// <param name="startedAtUtc">Data de início.</param>
    /// <param name="finishedAtUtc">Data de conclusão.</param>
    /// <param name="errorCode">Código de erro.</param>
    /// <param name="errorMessage">Mensagem de erro.</param>
    /// <param name="providerMessageId">Identificador retornado pelo provedor.</param>
    private DeliveryAttempt(
        Guid id,
        Guid notificationId,
        string provider,
        DeliveryAttemptStatus status,
        int attemptNumber,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        string? errorCode,
        string? errorMessage,
        string? providerMessageId)
        : base(id)
    {
        NotificationId = notificationId;
        Provider = Normalize(provider);
        Status = status;
        AttemptNumber = attemptNumber;
        StartedAtUtc = startedAtUtc;
        FinishedAtUtc = finishedAtUtc;
        ErrorCode = Normalize(errorCode);
        ErrorMessage = Normalize(errorMessage);
        ProviderMessageId = Normalize(providerMessageId);

        Validate();
    }



    /// <summary>
    /// Operação para registrar tentativa bem-sucedida.
    /// </summary>
    /// <param name="notificationId">Identificador da notificação.</param>
    /// <param name="provider">Provedor usado na tentativa.</param>
    /// <param name="attemptNumber">Número sequencial da tentativa.</param>
    /// <param name="startedAtUtc">Data de início.</param>
    /// <param name="finishedAtUtc">Data de conclusão.</param>
    /// <param name="providerMessageId">Identificador retornado pelo provedor.</param>
    /// <returns>Instância criada de <see cref="DeliveryAttempt"/>.</returns>
    internal static DeliveryAttempt RegisterSuccess(
        Guid notificationId,
        string provider,
        int attemptNumber,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        string? providerMessageId)
    {
        return new DeliveryAttempt(
            notificationId,
            provider,
            DeliveryAttemptStatus.Succeeded,
            attemptNumber,
            startedAtUtc,
            finishedAtUtc,
            errorCode: null,
            errorMessage: null,
            providerMessageId);
    }

    /// <summary>
    /// Operação para registrar tentativa com falha.
    /// </summary>
    /// <param name="notificationId">Identificador da notificação.</param>
    /// <param name="provider">Provedor usado na tentativa.</param>
    /// <param name="attemptNumber">Número sequencial da tentativa.</param>
    /// <param name="startedAtUtc">Data de início.</param>
    /// <param name="finishedAtUtc">Data de conclusão.</param>
    /// <param name="errorCode">Código de erro.</param>
    /// <param name="errorMessage">Mensagem de erro.</param>
    /// <returns>Instância criada de <see cref="DeliveryAttempt"/>.</returns>
    internal static DeliveryAttempt RegisterFailure(
        Guid notificationId,
        string provider,
        int attemptNumber,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        string? errorCode,
        string errorMessage)
    {
        return new DeliveryAttempt(
            notificationId,
            provider,
            DeliveryAttemptStatus.Failed,
            attemptNumber,
            startedAtUtc,
            finishedAtUtc,
            errorCode,
            errorMessage,
            providerMessageId: null);
    }

    /// <summary>
    /// Operação para restaurar tentativa persistida.
    /// </summary>
    /// <param name="id">Identificador da tentativa.</param>
    /// <param name="notificationId">Identificador da notificação.</param>
    /// <param name="provider">Provedor usado na tentativa.</param>
    /// <param name="status">Status da tentativa.</param>
    /// <param name="attemptNumber">Número sequencial da tentativa.</param>
    /// <param name="startedAtUtc">Data de início.</param>
    /// <param name="finishedAtUtc">Data de conclusão.</param>
    /// <param name="errorCode">Código de erro.</param>
    /// <param name="errorMessage">Mensagem de erro.</param>
    /// <param name="providerMessageId">Identificador retornado pelo provedor.</param>
    /// <returns>Instância restaurada de <see cref="DeliveryAttempt"/>.</returns>
    public static DeliveryAttempt Restore(
        Guid id,
        Guid notificationId,
        string provider,
        DeliveryAttemptStatus status,
        int attemptNumber,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        string? errorCode,
        string? errorMessage,
        string? providerMessageId)
    {
        return new DeliveryAttempt(
            id,
            notificationId,
            provider,
            status,
            attemptNumber,
            startedAtUtc,
            finishedAtUtc,
            errorCode,
            errorMessage,
            providerMessageId);
    }



    /// <summary>
    /// Operação para validar os dados da tentativa.
    /// </summary>
    private void Validate()
    {
        DomainException.When(Id == Guid.Empty, "O identificador da tentativa é obrigatório.");
        DomainException.When(NotificationId == Guid.Empty, "O identificador da notificação é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(Provider), "O provedor da tentativa é obrigatório.");
        DomainException.When(!Enum.IsDefined(typeof(DeliveryAttemptStatus), Status), "Status da tentativa inválido.");
        DomainException.When(AttemptNumber <= 0, "O número da tentativa deve ser maior que zero.");
        ValidateUtc(StartedAtUtc, "A data de início da tentativa é obrigatória e deve estar em UTC.");
        ValidateUtc(FinishedAtUtc, "A data de conclusão da tentativa é obrigatória e deve estar em UTC.");
        DomainException.When(FinishedAtUtc < StartedAtUtc, "A data de conclusão não pode ser anterior ao início da tentativa.");
        DomainException.When(Status == DeliveryAttemptStatus.Succeeded && (!string.IsNullOrWhiteSpace(ErrorCode) || !string.IsNullOrWhiteSpace(ErrorMessage)), "Tentativa bem-sucedida não deve possuir erro.");
        DomainException.When(Status == DeliveryAttemptStatus.Failed && string.IsNullOrWhiteSpace(ErrorMessage), "A mensagem de erro é obrigatória para tentativa com falha.");
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

}
