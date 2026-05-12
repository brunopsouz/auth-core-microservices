namespace NotificationCore.Domain.Notifications.Rendering;

/// <summary>
/// Representa template renderizado para envio.
/// </summary>
public sealed class RenderedTemplate
{
    /// <summary>
    /// Assunto renderizado.
    /// </summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// Corpo HTML renderizado.
    /// </summary>
    public string HtmlBody { get; init; } = string.Empty;

    /// <summary>
    /// Corpo texto renderizado.
    /// </summary>
    public string TextBody { get; init; } = string.Empty;
}
