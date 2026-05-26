using MailKit.Security;
using MimeKit;

namespace NotificationCore.Infrastructure.Notifications.Providers;

internal interface ISmtpClientAdapter : IAsyncDisposable
{
    int Timeout { get; set; }

    Task ConnectAsync(
        string host,
        int port,
        SecureSocketOptions options,
        CancellationToken cancellationToken);

    Task AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken);

    Task<string> SendAsync(
        MimeMessage message,
        CancellationToken cancellationToken);

    Task DisconnectAsync(
        bool quit,
        CancellationToken cancellationToken);
}
