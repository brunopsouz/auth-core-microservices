namespace NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;

/// <summary>
/// Define operacao para recuperar notificacoes com processamento expirado.
/// </summary>
internal interface ITimedOutNotificationRecovery
{
    /// <summary>
    /// Operacao para recuperar notificacoes com processamento expirado.
    /// </summary>
    /// <param name="command">Comando com os criterios do despacho.</param>
    /// <param name="counters">Contadores do processamento.</param>
    Task RecoverAsync(DispatchPendingNotificationCommand command, DispatchCounters counters);
}
