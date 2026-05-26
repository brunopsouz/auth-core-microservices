namespace NotificationCore.Application.UseCases.Notifications.RegisterNotificationRequest;

/// <summary>
/// Define operação para registrar solicitação de notificação.
/// </summary>
public interface IRegisterNotificationRequestUseCase
{
    /// <summary>
    /// Operação para registrar solicitação de notificação.
    /// </summary>
    /// <param name="command">Comando com a mensagem consumida.</param>
    /// <returns>Resultado do registro da solicitação.</returns>
    Task<RegisterNotificationRequestResult> Execute(RegisterNotificationRequestCommand command);
}
