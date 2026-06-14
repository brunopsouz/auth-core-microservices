using NotificationCore.Domain.Notifications.Templates;

namespace NotificationCore.Application.UseCases.Notifications.ListActiveNotificationTemplates;

/// <summary>
/// Representa resultado de leitura de template ativo.
/// </summary>
public sealed class ListActiveNotificationTemplateResult
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

    /// <summary>
    /// Operacao para criar resultado a partir do template.
    /// </summary>
    /// <param name="template">Template ativo.</param>
    /// <returns>Resultado de aplicacao.</returns>
    public static ListActiveNotificationTemplateResult FromTemplate(NotificationTemplateSummary template)
    {
        return new ListActiveNotificationTemplateResult
        {
            TemplateKey = template.TemplateKey,
            Channel = template.Channel,
            Subject = template.Subject,
            HtmlBody = template.HtmlBody,
            TextBody = template.TextBody
        };
    }
}
