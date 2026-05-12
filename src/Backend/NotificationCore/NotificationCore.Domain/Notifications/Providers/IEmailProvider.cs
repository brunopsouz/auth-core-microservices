namespace NotificationCore.Domain.Notifications.Providers;

/// <summary>
/// Define operação para enviar mensagem de e-mail.
/// </summary>
public interface IEmailProvider
{
    /// <summary>
    /// Operação para enviar mensagem de e-mail.
    /// </summary>
    /// <param name="message">Mensagem a ser enviada.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>Resultado do provedor de e-mail.</returns>
    Task<EmailProviderResult> SendAsync(
        EmailProviderMessage message,
        CancellationToken cancellationToken = default);
}
