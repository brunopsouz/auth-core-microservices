namespace NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;

/// <summary>
/// Define operacao para despachar notificacoes pendentes.
/// </summary>
internal interface IPendingNotificationDispatcher
{
    /// <summary>
    /// Operacao para despachar notificacoes pendentes.
    /// </summary>
    /// <param name="command">Comando com os criterios do despacho.</param>
    /// <param name="counters">Contadores do processamento.</param>
    Task DispatchAsync(DispatchPendingNotificationCommand command, DispatchCounters counters);
}
