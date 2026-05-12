using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Domain.Notifications.Rendering;

/// <summary>
/// Define operação para renderizar template de notificação.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Operação para renderizar template de notificação.
    /// </summary>
    /// <param name="templateKey">Chave do template.</param>
    /// <param name="channel">Canal da notificação.</param>
    /// <param name="variables">Variáveis usadas na renderização.</param>
    /// <returns>Template renderizado.</returns>
    Task<RenderedTemplate> RenderAsync(
        string templateKey,
        NotificationChannel channel,
        IReadOnlyDictionary<string, string> variables);
}
