namespace NotificationCore.Api.Observability;

/// <summary>
/// Representa resultado técnico do span de despacho.
/// </summary>
internal enum NotificationDispatchTelemetryResult
{
    /// <summary>
    /// Processamento concluído com sucesso.
    /// </summary>
    Success = 1,

    /// <summary>
    /// Processamento concluído com falha.
    /// </summary>
    Failure = 2,

    /// <summary>
    /// Processamento cancelado.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// Processamento concluiu com retry agendado.
    /// </summary>
    RetryScheduled = 4
}
