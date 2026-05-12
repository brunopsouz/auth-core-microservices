namespace NotificationCore.Domain.Notifications.Providers;

/// <summary>
/// Representa mensagem enviada ao provedor de e-mail.
/// </summary>
public sealed class EmailProviderMessage
{
    /// <summary>
    /// Identificador da notificação.
    /// </summary>
    public Guid NotificationId { get; init; }

    /// <summary>
    /// Identificador de correlação.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Destinatário do e-mail.
    /// </summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>
    /// Assunto do e-mail.
    /// </summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// Corpo HTML do e-mail.
    /// </summary>
    public string HtmlBody { get; init; } = string.Empty;

    /// <summary>
    /// Corpo texto do e-mail.
    /// </summary>
    public string TextBody { get; init; } = string.Empty;
}
