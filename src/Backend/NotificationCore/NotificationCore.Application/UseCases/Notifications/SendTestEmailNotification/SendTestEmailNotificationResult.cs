namespace NotificationCore.Application.UseCases.Notifications.SendTestEmailNotification;

/// <summary>
/// Representa resultado do envio de e-mail de teste.
/// </summary>
public sealed class SendTestEmailNotificationResult
{
    /// <summary>
    /// Identificador lógico do envio de teste.
    /// </summary>
    public Guid NotificationId { get; init; }

    /// <summary>
    /// Identificador de correlação usado no envio.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Destinatário do e-mail de teste.
    /// </summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>
    /// Nome do provedor utilizado.
    /// </summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Indica se o envio foi concluído com sucesso.
    /// </summary>
    public bool WasSent { get; init; }

    /// <summary>
    /// Indica se a falha permite nova tentativa.
    /// </summary>
    public bool IsTemporaryFailure { get; init; }

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
}
