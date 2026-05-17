namespace NotificationCore.Api.Contracts.Responses;

/// <summary>
/// Representa resposta com dados de template de notificação.
/// </summary>
public sealed class ResponseNotificationTemplateJson
{
    /// <summary>
    /// Chave do template.
    /// </summary>
    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>
    /// Canal da notificação.
    /// </summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>
    /// Assunto do template.
    /// </summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// Corpo HTML do template.
    /// </summary>
    public string HtmlBody { get; init; } = string.Empty;

    /// <summary>
    /// Corpo texto do template.
    /// </summary>
    public string TextBody { get; init; } = string.Empty;
}
