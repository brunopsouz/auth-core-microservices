using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NotificationCore.Api.HealthChecks;
using NotificationCore.Infrastructure.Configurations;

namespace NotificationCore.IntegrationTests.HealthChecks;

public sealed class SmtpHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenLoopbackSmtpAcceptsConnection_ShouldNotSendEmail()
    {
        await using var server = await LoopbackSmtpServer.StartAsync();
        var healthCheck = new SmtpHealthCheck(
            Options.Create(new SmtpOptions
            {
                Host = IPAddress.Loopback.ToString(),
                Port = server.Port,
                Username = "health-smtp-user-sentinel",
                Password = "health-smtp-password-sentinel",
                UseTls = false,
                TimeoutSeconds = 2
            }),
            Options.Create(new NotificationDispatcherOptions
            {
                Enabled = true
            }));
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "smtp",
                healthCheck,
                HealthStatus.Degraded,
                [Shared.Observability.HealthCheckTags.Dependency, Shared.Observability.HealthCheckTags.Optional])
        };

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.DoesNotContain("AUTH", server.Commands, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("MAIL FROM", server.Commands, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("RCPT TO", server.Commands, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("DATA", server.Commands, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDispatcherIsDisabled_ShouldNotConnect()
    {
        await using var server = await LoopbackSmtpServer.StartAsync();
        var healthCheck = new SmtpHealthCheck(
            Options.Create(new SmtpOptions
            {
                Host = IPAddress.Loopback.ToString(),
                Port = server.Port,
                TimeoutSeconds = 2
            }),
            Options.Create(new NotificationDispatcherOptions
            {
                Enabled = false
            }));
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "smtp",
                healthCheck,
                HealthStatus.Degraded,
                [Shared.Observability.HealthCheckTags.Dependency, Shared.Observability.HealthCheckTags.Optional])
        };

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Empty(server.Commands);
    }

    private sealed class LoopbackSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _serverTask;
        private readonly List<string> _commands = [];

        private LoopbackSmtpServer(TcpListener listener)
        {
            _listener = listener;
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serverTask = AcceptAsync();
        }

        public int Port { get; }

        public IReadOnlyList<string> Commands => _commands;

        public static Task<LoopbackSmtpServer> StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, port: 0);
            listener.Start();

            return Task.FromResult(new LoopbackSmtpServer(listener));
        }

        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync();
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
            finally
            {
                _stopping.Dispose();
            }
        }

        private async Task AcceptAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            await using var writer = new StreamWriter(stream)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };

            await writer.WriteLineAsync("220 localhost ESMTP");

            while (!_stopping.IsCancellationRequested)
            {
                var command = await reader.ReadLineAsync(_stopping.Token);
                if (command is null)
                    return;

                _commands.Add(command);

                if (command.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase)
                    || command.StartsWith("HELO ", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250-localhost");
                    await writer.WriteLineAsync("250 OK");
                    continue;
                }

                if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 Bye");
                    return;
                }

                await writer.WriteLineAsync("250 OK");
            }
        }
    }
}
