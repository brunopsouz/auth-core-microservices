using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Api.Observability;

/// <summary>
/// Representa contexto técnico seguro para telemetria de despacho.
/// </summary>
internal sealed class NotificationDispatchTelemetryContext
{
    /// <summary>
    /// Chave funcional do template.
    /// </summary>
    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>
    /// Canal funcional da notificação.
    /// </summary>
    public NotificationChannel Channel { get; init; }

    /// <summary>
    /// Indica se o despacho é uma nova tentativa.
    /// </summary>
    public bool IsRetry { get; init; }

    /// <summary>
    /// Contexto W3C persistido na entrada original.
    /// </summary>
    public string? TraceParent { get; init; }

    /// <summary>
    /// Estado W3C persistido na entrada original.
    /// </summary>
    public string? TraceState { get; init; }
}
