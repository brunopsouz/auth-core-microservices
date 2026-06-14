namespace NotificationCore.Domain.Notifications.Templates;

/// <summary>
/// Representa dados de leitura de um template de notificacao.
/// </summary>
public sealed class NotificationTemplateSummary
{
    /// <summary>
    /// Chave do template.
    /// </summary>
    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>
    /// Canal de entrega.
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
