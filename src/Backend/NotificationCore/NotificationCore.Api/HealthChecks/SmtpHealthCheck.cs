using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NotificationCore.Infrastructure.Configurations;

namespace NotificationCore.Api.HealthChecks;

/// <summary>
/// Representa health check para conectividade SMTP.
/// </summary>
internal sealed class SmtpHealthCheck : IHealthCheck
{
    /// <summary>
    /// Campo que armazena options SMTP.
    /// </summary>
    private readonly SmtpOptions _smtpOptions;
    /// <summary>
    /// Campo que armazena options do dispatcher.
    /// </summary>
    private readonly NotificationDispatcherOptions _dispatcherOptions;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="smtpOptions">Opções SMTP.</param>
    /// <param name="dispatcherOptions">Opções do dispatcher.</param>
    public SmtpHealthCheck(
        IOptions<SmtpOptions> smtpOptions,
        IOptions<NotificationDispatcherOptions> dispatcherOptions)
    {
        ArgumentNullException.ThrowIfNull(smtpOptions);
        ArgumentNullException.ThrowIfNull(dispatcherOptions);

        _smtpOptions = smtpOptions.Value;
        _dispatcherOptions = dispatcherOptions.Value;
    }

    /// <summary>
    /// Operação para verificar conectividade SMTP.
    /// </summary>
    /// <param name="context">Contexto da execução do health check.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Resultado do health check executado.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_dispatcherOptions.Enabled)
            return HealthCheckResult.Healthy();

        try
        {
            using var client = new SmtpClient
            {
                Timeout = Math.Max(1, _smtpOptions.TimeoutSeconds) * 1000
            };

            await client.ConnectAsync(
                _smtpOptions.Host,
                _smtpOptions.Port,
                GetSecureSocketOptions(_smtpOptions),
                cancellationToken);

            await client.DisconnectAsync(true, cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch
        {
            return new HealthCheckResult(context.Registration.FailureStatus);
        }
    }

    private static SecureSocketOptions GetSecureSocketOptions(SmtpOptions options)
    {
        if (!options.UseTls)
            return SecureSocketOptions.None;

        return options.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }
}
