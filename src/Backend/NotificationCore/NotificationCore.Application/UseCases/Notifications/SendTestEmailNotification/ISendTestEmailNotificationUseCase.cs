namespace NotificationCore.Application.UseCases.Notifications.SendTestEmailNotification;

/// <summary>
/// Define operação para enviar e-mail de teste.
/// </summary>
public interface ISendTestEmailNotificationUseCase
{
    /// <summary>
    /// Operação para enviar e-mail de teste.
    /// </summary>
    /// <param name="command">Comando com os dados do e-mail de teste.</param>
    /// <returns>Resultado do envio de teste.</returns>
    Task<SendTestEmailNotificationResult> Execute(SendTestEmailNotificationCommand command);
}
