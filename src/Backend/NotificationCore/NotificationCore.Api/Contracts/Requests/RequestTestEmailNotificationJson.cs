namespace NotificationCore.Api.Contracts.Requests;

/// <summary>
/// Representa requisição para envio de e-mail de teste.
/// </summary>
public sealed class RequestTestEmailNotificationJson
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
