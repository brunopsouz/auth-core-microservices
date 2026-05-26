namespace NotificationCore.Application.UseCases.Notifications.SendTestEmailNotification;

/// <summary>
/// Representa comando para enviar e-mail de teste.
/// </summary>
public sealed class SendTestEmailNotificationCommand
{
    /// <summary>
    /// Destinatário do e-mail de teste.
    /// </summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>
    /// Identificador de correlação opcional.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;
}
