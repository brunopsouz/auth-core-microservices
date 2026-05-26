using NotificationCore.Domain.Notifications.Entities;

namespace NotificationCore.Application.UseCases.Notifications.Models;

/// <summary>
/// Representa tentativa de entrega de uma notificação.
/// </summary>
public sealed class NotificationDeliveryAttemptResult
{
    /// <summary>
    /// Identificador da tentativa.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nome do provedor utilizado.
    /// </summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Status da tentativa.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Número sequencial da tentativa.
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// Data e hora UTC de início da tentativa.
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// Data e hora UTC de conclusão da tentativa.
    /// </summary>
    public DateTime FinishedAtUtc { get; init; }

    /// <summary>
    /// Código de erro sanitizado.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Mensagem de erro sanitizada.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Identificador retornado pelo provedor.
    /// </summary>
    public string ProviderMessageId { get; init; } = string.Empty;

    /// <summary>
    /// Operação para criar resultado a partir da tentativa.
    /// </summary>
    /// <param name="deliveryAttempt">Tentativa de entrega.</param>
    /// <returns>Resultado da tentativa.</returns>
    public static NotificationDeliveryAttemptResult FromDeliveryAttempt(DeliveryAttempt deliveryAttempt)
    {
        ArgumentNullException.ThrowIfNull(deliveryAttempt);

        return new NotificationDeliveryAttemptResult
        {
            Id = deliveryAttempt.Id,
            Provider = deliveryAttempt.Provider,
            Status = deliveryAttempt.Status.ToString(),
            AttemptNumber = deliveryAttempt.AttemptNumber,
            StartedAtUtc = deliveryAttempt.StartedAtUtc,
            FinishedAtUtc = deliveryAttempt.FinishedAtUtc,
            ErrorCode = deliveryAttempt.ErrorCode,
            ErrorMessage = deliveryAttempt.ErrorMessage,
            ProviderMessageId = deliveryAttempt.ProviderMessageId
        };
    }
}
