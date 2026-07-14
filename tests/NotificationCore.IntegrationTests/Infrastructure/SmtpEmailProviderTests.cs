using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NotificationCore.Domain.Notifications.Providers;
using NotificationCore.Infrastructure.Configurations;
using NotificationCore.Infrastructure.Notifications.Providers;

namespace NotificationCore.IntegrationTests.Infrastructure;

using SystemAuthenticationException = System.Security.Authentication.AuthenticationException;

public sealed class SmtpEmailProviderTests
{
    [Fact]
    public async Task SendAsync_WhenLoopbackSmtpAcceptsMessage_ShouldReturnSuccessWithoutExternalServer()
    {
        await using var server = await LoopbackSmtpServer.StartAsync();
        var provider = new SmtpEmailProvider(
            Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = server.Port,
                Username = string.Empty,
                Password = "smtp-password-sentinel-observability",
                UseTls = false,
                SenderEmail = "notifications@example.com",
                SenderName = "NotificationCore",
                TimeoutSeconds = 10
            }),
            new MailKitSmtpClientFactory());

        var result = await provider.SendAsync(CreateSentinelMessage());

        Assert.True(result.IsSuccess);
        Assert.Contains("smtp-subject-sentinel-observability", server.Data, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("smtp-body-sentinel-observability", server.Data, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpSendSucceeds_ShouldReturnSuccessAndUseConfiguredSender()
    {
        var smtpClient = new FakeSmtpClientAdapter
        {
            SendResponse = "smtp-message-123"
        };
        var provider = CreateProvider(smtpClient);

        var result = await provider.SendAsync(CreateMessage());
        var sentMessage = Assert.IsType<MimeMessage>(smtpClient.SentMessage);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsTemporaryFailure);
        Assert.Equal("Smtp", result.Provider);
        Assert.Equal(sentMessage.MessageId, result.ProviderMessageId);
        Assert.True(smtpClient.Authenticated);
        Assert.True(smtpClient.Disconnected);
        Assert.True(smtpClient.Disposed);
        Assert.Equal("localhost", smtpClient.Host);
        Assert.Equal(587, smtpClient.Port);
        Assert.Equal(SecureSocketOptions.StartTls, smtpClient.SecureSocketOptions);
        Assert.Equal(10000, smtpClient.Timeout);
        Assert.Equal("notifications@example.com", sentMessage.From.Mailboxes.Single().Address);
        Assert.Equal("user@example.com", sentMessage.To.Mailboxes.Single().Address);
        Assert.Equal("correlation-123", sentMessage.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpReturnsLongResponse_ShouldPersistControlledMimeMessageId()
    {
        var smtpClient = new FakeSmtpClientAdapter
        {
            SendResponse = new string('A', 500)
        };
        var provider = CreateProvider(smtpClient);

        var result = await provider.SendAsync(CreateMessage());
        var sentMessage = Assert.IsType<MimeMessage>(smtpClient.SentMessage);

        Assert.True(result.IsSuccess);
        Assert.Equal(sentMessage.MessageId, result.ProviderMessageId);
        Assert.True(result.ProviderMessageId.Length <= 300);
        Assert.NotEqual(smtpClient.SendResponse, result.ProviderMessageId);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpHasTemporaryFailure_ShouldReturnSanitizedTemporaryFailure()
    {
        var smtpClient = new FakeSmtpClientAdapter
        {
            ExceptionOnSend = new IOException("Servidor indisponível para enviar corpo com código 123456.")
        };
        var provider = CreateProvider(smtpClient);

        var result = await provider.SendAsync(CreateMessage());

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTemporaryFailure);
        Assert.Equal("Smtp", result.Provider);
        Assert.Equal("SMTP_TEMPORARY_FAILURE", result.ErrorCode);
        Assert.Equal("Falha temporária no SMTP.", result.ErrorMessage);
        Assert.DoesNotContain("123456", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WhenAuthenticationFails_ShouldReturnSanitizedPermanentFailure()
    {
        var smtpClient = new FakeSmtpClientAdapter
        {
            ExceptionOnAuthenticate = new SystemAuthenticationException("Senha inválida para código 123456.")
        };
        var provider = CreateProvider(smtpClient);

        var result = await provider.SendAsync(CreateMessage());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTemporaryFailure);
        Assert.Equal("Smtp", result.Provider);
        Assert.Equal("SMTP_AUTHENTICATION_FAILED", result.ErrorCode);
        Assert.Equal("Falha permanente no SMTP.", result.ErrorMessage);
        Assert.DoesNotContain("123456", result.ErrorMessage);
        Assert.Null(smtpClient.SentMessage);
    }

    private static SmtpEmailProvider CreateProvider(FakeSmtpClientAdapter smtpClient)
    {
        return new SmtpEmailProvider(
            Options.Create(new SmtpOptions
            {
                Host = "localhost",
                Port = 587,
                Username = "smtp-user",
                Password = "smtp-password",
                UseTls = true,
                SenderEmail = "notifications@example.com",
                SenderName = "NotificationCore",
                TimeoutSeconds = 10
            }),
            new FakeSmtpClientFactory(smtpClient),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SmtpEmailProvider>.Instance);
    }

    private static EmailProviderMessage CreateMessage()
    {
        return new EmailProviderMessage
        {
            NotificationId = Guid.Parse("2d2b1eb0-7db5-4077-bb03-09c815ecde78"),
            CorrelationId = "correlation-123",
            Recipient = "user@example.com",
            Subject = "Confirme seu e-mail",
            HtmlBody = "<p>Seu código é 123456.</p>",
            TextBody = "Seu código é 123456."
        };
    }

    private static EmailProviderMessage CreateSentinelMessage()
    {
        return new EmailProviderMessage
        {
            NotificationId = Guid.Parse("2d2b1eb0-7db5-4077-bb03-09c815ecde78"),
            CorrelationId = "correlation-123",
            Recipient = "smtp-recipient-sentinel@example.invalid",
            Subject = "smtp-subject-sentinel-observability",
            HtmlBody = "<p>smtp-body-sentinel-observability smtp-token-sentinel-observability</p>",
            TextBody = "smtp-body-sentinel-observability smtp-token-sentinel-observability"
        };
    }

    private sealed class FakeSmtpClientFactory : ISmtpClientFactory
    {
        /// <summary>
        /// Campo que armazena smtp client.
        /// </summary>
        private readonly ISmtpClientAdapter _smtpClient;

        public FakeSmtpClientFactory(ISmtpClientAdapter smtpClient)
        {
            _smtpClient = smtpClient;
        }

        public ISmtpClientAdapter Create()
        {
            return _smtpClient;
        }
    }

    private sealed class FakeSmtpClientAdapter : ISmtpClientAdapter
    {
        public Exception? ExceptionOnAuthenticate { get; init; }

        public Exception? ExceptionOnSend { get; init; }

        public string SendResponse { get; init; } = string.Empty;

        public int Timeout { get; set; }

        public string Host { get; private set; } = string.Empty;

        public int Port { get; private set; }

        public SecureSocketOptions SecureSocketOptions { get; private set; }

        public bool Authenticated { get; private set; }

        public bool Disconnected { get; private set; }

        public bool Disposed { get; private set; }

        public MimeMessage? SentMessage { get; private set; }

        public Task ConnectAsync(
            string host,
            int port,
            SecureSocketOptions options,
            CancellationToken cancellationToken)
        {
            Host = host;
            Port = port;
            SecureSocketOptions = options;

            return Task.CompletedTask;
        }

        public Task AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            if (ExceptionOnAuthenticate is not null)
                throw ExceptionOnAuthenticate;

            Authenticated = true;

            return Task.CompletedTask;
        }

        public Task<string> SendAsync(
            MimeMessage message,
            CancellationToken cancellationToken)
        {
            SentMessage = message;

            if (ExceptionOnSend is not null)
                throw ExceptionOnSend;

            return Task.FromResult(SendResponse);
        }

        public Task DisconnectAsync(
            bool quit,
            CancellationToken cancellationToken)
        {
            Disconnected = quit;

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class LoopbackSmtpServer : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly TcpListener _listener;
        private readonly Task _serverTask;

        private LoopbackSmtpServer(TcpListener listener)
        {
            _listener = listener;
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serverTask = AcceptAsync(_cancellationTokenSource.Token);
        }

        public int Port { get; }

        public string Data { get; private set; } = string.Empty;

        public static Task<LoopbackSmtpServer> StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, port: 0);
            listener.Start();

            return Task.FromResult(new LoopbackSmtpServer(listener));
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellationTokenSource.CancelAsync();
            _listener.Stop();

            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }

            _cancellationTokenSource.Dispose();
        }

        private async Task AcceptAsync(CancellationToken cancellationToken)
        {
            using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };

            await writer.WriteLineAsync("220 localhost ESMTP");

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line is null)
                    return;

                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250-localhost");
                    await writer.WriteLineAsync("250 OK");
                    continue;
                }

                if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250 OK");
                    continue;
                }

                if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    Data = await ReadDataAsync(reader, cancellationToken);
                    await writer.WriteLineAsync("250 queued");
                    continue;
                }

                if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 bye");
                    return;
                }

                await writer.WriteLineAsync("250 OK");
            }
        }

        private static async Task<string> ReadDataAsync(
            StreamReader reader,
            CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line is null || line == ".")
                    break;

                builder.AppendLine(line);
            }

            return builder.ToString();
        }
    }
}
