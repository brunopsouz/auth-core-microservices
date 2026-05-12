namespace NotificationCore.Application.Notifications.UseCases.DispatchPendingNotification;

/// <summary>
/// Define operação para despachar notificações pendentes.
/// </summary>
public interface IDispatchPendingNotificationUseCase
{
    /// <summary>
    /// Operação para despachar notificações pendentes.
    /// </summary>
    /// <param name="command">Comando com os critérios de despacho.</param>
    /// <returns>Resultado do despacho.</returns>
    Task<DispatchPendingNotificationResult> Execute(DispatchPendingNotificationCommand command);
}
