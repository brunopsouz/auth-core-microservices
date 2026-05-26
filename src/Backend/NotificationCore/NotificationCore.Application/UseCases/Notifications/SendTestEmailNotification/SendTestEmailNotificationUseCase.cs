using NotificationCore.Domain.Notifications.Providers;
using NotificationCore.Domain.Notifications.ValueObjects;

namespace NotificationCore.Application.UseCases.Notifications.SendTestEmailNotification;

/// <summary>
/// Representa caso de uso para enviar e-mail de teste.
/// </summary>
internal sealed class SendTestEmailNotificationUseCase : ISendTestEmailNotificationUseCase
{
    private const string DEFAULT_PROVIDER = "EmailProvider";
    private const string TEST_SUBJECT = "Teste de notificação";
    private const string TEST_TEXT_BODY = "Mensagem de teste do NotificationCore.";
    private const string TEST_HTML_BODY = "<p>Mensagem de teste do NotificationCore.</p>";

    /// <summary>
    /// Campo que armazena email provider.
    /// </summary>
    private readonly IEmailProvider _emailProvider;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="emailProvider">Provedor de e-mail.</param>
    public SendTestEmailNotificationUseCase(IEmailProvider emailProvider)
    {
        ArgumentNullException.ThrowIfNull(emailProvider);

        _emailProvider = emailProvider;
    }


    /// <summary>
    /// Operação para enviar e-mail de teste.
    /// </summary>
    /// <param name="command">Comando com os dados do e-mail de teste.</param>
    /// <returns>Resultado do envio de teste.</returns>
    public async Task<SendTestEmailNotificationResult> Execute(SendTestEmailNotificationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var recipient = RecipientEmail.Create(command.Recipient);
        var notificationId = Guid.NewGuid();
        var correlationId = CreateCorrelationId(command.CorrelationId, notificationId);
        var result = await _emailProvider.SendAsync(new EmailProviderMessage
        {
            NotificationId = notificationId,
            CorrelationId = correlationId,
            Recipient = recipient.Value,
            Subject = TEST_SUBJECT,
            HtmlBody = TEST_HTML_BODY,
            TextBody = TEST_TEXT_BODY
        });

        return new SendTestEmailNotificationResult
        {
            NotificationId = notificationId,
            CorrelationId = correlationId,
            Recipient = recipient.Value,
            Provider = NormalizeProvider(result.Provider),
            WasSent = result.IsSuccess,
            IsTemporaryFailure = result.IsTemporaryFailure,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage,
            ProviderMessageId = result.ProviderMessageId
        };
    }


    /// <summary>
    /// Operação para criar identificador de correlação.
    /// </summary>
    /// <param name="correlationId">Identificador informado.</param>
    /// <param name="notificationId">Identificador lógico do envio.</param>
    /// <returns>Identificador de correlação normalizado.</returns>
    private static string CreateCorrelationId(string correlationId, Guid notificationId)
    {
        return string.IsNullOrWhiteSpace(correlationId)
            ? $"notificationcore-test:{notificationId}"
            : correlationId.Trim();
    }

    /// <summary>
    /// Operação para normalizar provedor.
    /// </summary>
    /// <param name="provider">Provedor informado.</param>
    /// <returns>Provedor normalizado.</returns>
    private static string NormalizeProvider(string provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? DEFAULT_PROVIDER
            : provider.Trim();
    }

}
