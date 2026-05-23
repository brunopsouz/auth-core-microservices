using AuthCore.Domain.Security.Emails;
using Microsoft.Extensions.Logging;

namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa sender inicial de e-mail com logging.
/// </summary>
internal sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="logger">Serviço de logging.</param>
    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    #endregion

    /// <summary>
    /// Operação para enviar o código de verificação de e-mail.
    /// </summary>
    /// <param name="email">E-mail de destino.</param>
    /// <param name="code">Código OTP em texto puro.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    public Task SendEmailVerificationAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Simulando envio de e-mail de verificação para {Email}.",
            email);

        return Task.CompletedTask;
    }
}
