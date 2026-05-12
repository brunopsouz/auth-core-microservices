using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NotificationCore.Infrastructure.Notifications.Providers;

internal sealed class MailKitSmtpClientFactory : ISmtpClientFactory
{
    public ISmtpClientAdapter Create()
    {
        return new MailKitSmtpClientAdapter(new SmtpClient());
    }

    private sealed class MailKitSmtpClientAdapter : ISmtpClientAdapter
    {
        private readonly SmtpClient _client;

        public int Timeout
        {
            get => _client.Timeout;
            set => _client.Timeout = value;
        }

        public MailKitSmtpClientAdapter(SmtpClient client)
        {
            _client = client;
        }

        public Task ConnectAsync(
            string host,
            int port,
            SecureSocketOptions options,
            CancellationToken cancellationToken)
        {
            return _client.ConnectAsync(host, port, options, cancellationToken);
        }

        public Task AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            return _client.AuthenticateAsync(username, password, cancellationToken);
        }

        public Task<string> SendAsync(
            MimeMessage message,
            CancellationToken cancellationToken)
        {
            return _client.SendAsync(message, cancellationToken);
        }

        public Task DisconnectAsync(
            bool quit,
            CancellationToken cancellationToken)
        {
            return _client.DisconnectAsync(quit, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _client.Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
