using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;
using NotificationCore.Domain.Notifications.Providers;
using NotificationCore.Infrastructure.Configurations;
using NotificationCore.Infrastructure.Observability;

namespace NotificationCore.Infrastructure.Notifications.Providers;

using MailKitAuthenticationException = MailKit.Security.AuthenticationException;
using SystemAuthenticationException = System.Security.Authentication.AuthenticationException;

/// <summary>
/// Representa provedor SMTP para envio de e-mails.
/// </summary>
internal sealed class SmtpEmailProvider : IEmailProvider
{
    private const string PROVIDER = "Smtp";
    private const string TEMPORARY_FAILURE_MESSAGE = "Falha temporária no SMTP.";
    private const string PERMANENT_FAILURE_MESSAGE = "Falha permanente no SMTP.";
    private const string MESSAGE_INVALID_CODE = "SMTP_MESSAGE_INVALID";
    private const string AUTHENTICATION_FAILED_CODE = "SMTP_AUTHENTICATION_FAILED";
    private const string TEMPORARY_FAILURE_CODE = "SMTP_TEMPORARY_FAILURE";
    private const string PERMANENT_FAILURE_CODE = "SMTP_PERMANENT_FAILURE";
    private const int MAX_PROVIDER_MESSAGE_ID_LENGTH = 300;

    /// <summary>
    /// Campo que armazena options.
    /// </summary>
    private readonly IOptions<SmtpOptions> _options;
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<SmtpEmailProvider> _logger;
    /// <summary>
    /// Campo que armazena notification metrics.
    /// </summary>
    private readonly NotificationMetrics _notificationMetrics;
    /// <summary>
    /// Campo que armazena smtp client factory.
    /// </summary>
    private readonly ISmtpClientFactory _smtpClientFactory;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="options">Configurações SMTP.</param>
    public SmtpEmailProvider(IOptions<SmtpOptions> options)
        : this(options, new MailKitSmtpClientFactory())
    {
    }

    internal SmtpEmailProvider(
        IOptions<SmtpOptions> options,
        ISmtpClientFactory smtpClientFactory)
        : this(
            options,
            smtpClientFactory,
            new NotificationMetrics(),
            NullLogger<SmtpEmailProvider>.Instance)
    {
    }

    internal SmtpEmailProvider(
        IOptions<SmtpOptions> options,
        ISmtpClientFactory smtpClientFactory,
        NotificationMetrics notificationMetrics,
        ILogger<SmtpEmailProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(smtpClientFactory);
        ArgumentNullException.ThrowIfNull(notificationMetrics);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _smtpClientFactory = smtpClientFactory;
        _notificationMetrics = notificationMetrics;
        _logger = logger;
    }


    /// <summary>
    /// Operação para enviar mensagem de e-mail.
    /// </summary>
    /// <param name="message">Mensagem a ser enviada.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>Resultado do provedor de e-mail.</returns>
    public async Task<EmailProviderResult> SendAsync(
        EmailProviderMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var stopwatch = Stopwatch.StartNew();
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlationId"] = message.CorrelationId,
            ["notificationId"] = message.NotificationId,
            ["provider"] = PROVIDER
        });

        try
        {
            var options = _options.Value;
            var mimeMessage = CreateMimeMessage(options, message);

            await using var client = _smtpClientFactory.Create();

            client.Timeout = options.TimeoutSeconds * 1000;

            await client.ConnectAsync(
                options.Host,
                options.Port,
                GetSecureSocketOptions(options),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(options.Username))
            {
                await client.AuthenticateAsync(
                    options.Username,
                    options.Password,
                    cancellationToken);
            }

            _ = await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation(
                "E-mail enviado via SMTP. NotificationId={NotificationId}, CorrelationId={CorrelationId}, Provider={Provider}.",
                message.NotificationId,
                message.CorrelationId,
                PROVIDER);

            return EmailProviderResult.Success(PROVIDER, NormalizeProviderMessageId(mimeMessage.MessageId));
        }
        catch (Exception exception) when (IsPermanentFailure(exception))
        {
            _logger.LogWarning(
                "Falha permanente no envio SMTP. NotificationId={NotificationId}, CorrelationId={CorrelationId}, Provider={Provider}, ExceptionType={ExceptionType}.",
                message.NotificationId,
                message.CorrelationId,
                PROVIDER,
                exception.GetType().Name);

            return EmailProviderResult.PermanentFailure(
                PROVIDER,
                GetErrorCode(exception),
                PERMANENT_FAILURE_MESSAGE);
        }
        catch (Exception exception) when (IsTemporaryFailure(exception))
        {
            _logger.LogWarning(
                "Falha temporária no envio SMTP. NotificationId={NotificationId}, CorrelationId={CorrelationId}, Provider={Provider}, ExceptionType={ExceptionType}.",
                message.NotificationId,
                message.CorrelationId,
                PROVIDER,
                exception.GetType().Name);

            return EmailProviderResult.TemporaryFailure(
                PROVIDER,
                GetErrorCode(exception),
                TEMPORARY_FAILURE_MESSAGE);
        }
        catch
        {
            _logger.LogWarning(
                "Falha permanente não classificada no envio SMTP. NotificationId={NotificationId}, CorrelationId={CorrelationId}, Provider={Provider}.",
                message.NotificationId,
                message.CorrelationId,
                PROVIDER);

            return EmailProviderResult.PermanentFailure(
                PROVIDER,
                PERMANENT_FAILURE_CODE,
                PERMANENT_FAILURE_MESSAGE);
        }
        finally
        {
            stopwatch.Stop();
            _notificationMetrics.RecordSendDuration(stopwatch.Elapsed, PROVIDER);
        }
    }


    /// <summary>
    /// Operação para criar a mensagem MIME.
    /// </summary>
    /// <param name="options">Configurações SMTP.</param>
    /// <param name="message">Mensagem do provedor.</param>
    /// <returns>Mensagem MIME pronta para envio.</returns>
    private static MimeMessage CreateMimeMessage(
        SmtpOptions options,
        EmailProviderMessage message)
    {
        var mimeMessage = new MimeMessage
        {
            Subject = message.Subject,
            MessageId = MimeUtils.GenerateMessageId()
        };

        mimeMessage.From.Add(new MailboxAddress(options.SenderName, options.SenderEmail));
        mimeMessage.To.Add(MailboxAddress.Parse(message.Recipient));

        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
            mimeMessage.Headers.Add("X-Correlation-Id", message.CorrelationId);

        mimeMessage.Headers.Add("X-Notification-Id", message.NotificationId.ToString("D"));
        mimeMessage.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        }.ToMessageBody();

        return mimeMessage;
    }

    /// <summary>
    /// Operação para obter a opção de segurança da conexão.
    /// </summary>
    /// <param name="options">Configurações SMTP.</param>
    /// <returns>Opção de segurança para MailKit.</returns>
    private static SecureSocketOptions GetSecureSocketOptions(SmtpOptions options)
    {
        if (!options.UseTls)
            return SecureSocketOptions.None;

        return options.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }

    /// <summary>
    /// Operação para indicar se a exceção representa falha permanente.
    /// </summary>
    /// <param name="exception">Exceção gerada pelo envio.</param>
    /// <returns>Verdadeiro quando a falha é permanente.</returns>
    private static bool IsPermanentFailure(Exception exception)
    {
        return exception is MailKitAuthenticationException
            or SystemAuthenticationException
            or ArgumentException
            or FormatException
            || exception is SmtpCommandException smtpCommandException
                && !IsTemporaryStatusCode(smtpCommandException.StatusCode);
    }

    /// <summary>
    /// Operação para indicar se a exceção representa falha temporária.
    /// </summary>
    /// <param name="exception">Exceção gerada pelo envio.</param>
    /// <returns>Verdadeiro quando a falha é temporária.</returns>
    private static bool IsTemporaryFailure(Exception exception)
    {
        return exception is IOException
            or SocketException
            or TimeoutException
            or OperationCanceledException
            or SmtpProtocolException
            || exception is SmtpCommandException smtpCommandException
                && IsTemporaryStatusCode(smtpCommandException.StatusCode);
    }

    /// <summary>
    /// Operação para indicar se o status SMTP é temporário.
    /// </summary>
    /// <param name="statusCode">Código de status SMTP.</param>
    /// <returns>Verdadeiro quando o status é temporário.</returns>
    private static bool IsTemporaryStatusCode(SmtpStatusCode statusCode)
    {
        var code = (int)statusCode;

        return code >= 400 && code < 500;
    }

    /// <summary>
    /// Operação para obter código de erro seguro para persistência.
    /// </summary>
    /// <param name="exception">Exceção gerada pelo envio.</param>
    /// <returns>Código de erro sem dados sensíveis.</returns>
    private static string GetErrorCode(Exception exception)
    {
        return exception switch
        {
            SmtpCommandException smtpCommandException => ((int)smtpCommandException.StatusCode).ToString(CultureInfo.InvariantCulture),
            MailKitAuthenticationException or SystemAuthenticationException => AUTHENTICATION_FAILED_CODE,
            ArgumentException or FormatException => MESSAGE_INVALID_CODE,
            IOException or SocketException or TimeoutException or OperationCanceledException or SmtpProtocolException => TEMPORARY_FAILURE_CODE,
            _ => PERMANENT_FAILURE_CODE
        };
    }

    /// <summary>
    /// Operação para normalizar o identificador da mensagem persistido.
    /// </summary>
    /// <param name="messageId">Identificador MIME da mensagem.</param>
    /// <returns>Identificador seguro para persistência.</returns>
    private static string NormalizeProviderMessageId(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return Guid.NewGuid().ToString("D");

        return messageId.Length <= MAX_PROVIDER_MESSAGE_ID_LENGTH
            ? messageId
            : messageId[..MAX_PROVIDER_MESSAGE_ID_LENGTH];
    }

}
