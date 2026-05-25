using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Infrastructure.Notifications.Templates;

/// <summary>
/// Representa template de notificação carregado da infraestrutura.
/// </summary>
internal sealed class NotificationTemplate
{
    /// <summary>
    /// Chave do template.
    /// </summary>
    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>
    /// Canal da notificação.
    /// </summary>
    public NotificationChannel Channel { get; init; }

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
