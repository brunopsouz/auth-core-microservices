using System.Text;
using System.Text.Json;
using Shared.Messaging.Contracts.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationCore.Api.Workers;
using NotificationCore.Application.UseCases.Notifications.RegisterNotificationRequest;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Infrastructure.Configurations;
using NotificationCore.Infrastructure.Messaging.RabbitMq;

namespace NotificationCore.IntegrationTests.Api;

public sealed class RabbitMqNotificationConsumerHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRabbitMqIsDisabled_ShouldNotStartConsumer()
    {
        var consumer = new FakeRabbitMqNotificationConsumer();
        var useCase = new SpyRegisterNotificationRequestUseCase();
        await using var serviceProvider = CreateServiceProvider(useCase);
        var hostedService = CreateHostedService(serviceProvider, consumer, new RabbitMqOptions
        {
            Enabled = false
        });

        await hostedService.StartAsync(CancellationToken.None);

        Assert.False(consumer.WasStarted);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessMessage_WhenRequestIsValid_ShouldAckAndExecuteUseCase()
    {
        var consumer = new FakeRabbitMqNotificationConsumer();
        var useCase = new SpyRegisterNotificationRequestUseCase();
        await using var serviceProvider = CreateServiceProvider(useCase);
        var hostedService = CreateHostedService(serviceProvider, consumer);

        await hostedService.StartAsync(CancellationToken.None);

        var request = CreateRequest();
        var disposition = await consumer.ProcessAsync(JsonSerializer.Serialize(request));

        Assert.Equal(RabbitMqNotificationDisposition.Ack, disposition);
        var receivedRequest = Assert.Single(useCase.Requests);
        Assert.Equal(request.MessageId, receivedRequest.MessageId);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessMessage_WhenRequestUsesCamelCasePayload_ShouldAckAndExecuteUseCase()
    {
        var consumer = new FakeRabbitMqNotificationConsumer();
        var useCase = new SpyRegisterNotificationRequestUseCase();
        await using var serviceProvider = CreateServiceProvider(useCase);
        var hostedService = CreateHostedService(serviceProvider, consumer);

        await hostedService.StartAsync(CancellationToken.None);

        var disposition = await consumer.ProcessAsync(
            """
            {
              "messageId": "0d4caa56-b276-46c8-98b5-4ab562206dea",
              "correlationId": "correlation-123",
              "source": "AuthCore",
              "channel": "Email",
              "recipient": "user@example.com",
              "templateKey": "auth.email-confirmation",
              "variables": {
                "confirmationCode": "123456",
                "expiresInMinutes": "15"
              },
              "priority": "High",
              "idempotencyKey": "auth-email-confirmation:5fbd8e65-3a5c-4789-8493-2706bb71f62b",
              "requestedAtUtc": "2026-05-11T12:00:00Z"
            }
            """);

        Assert.Equal(RabbitMqNotificationDisposition.Ack, disposition);
        var receivedRequest = Assert.Single(useCase.Requests);
        Assert.Equal(Guid.Parse("0d4caa56-b276-46c8-98b5-4ab562206dea"), receivedRequest.MessageId);
        Assert.Equal("auth.email-confirmation", receivedRequest.TemplateKey);
        Assert.Equal("123456", receivedRequest.Variables["confirmationCode"]);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessMessage_WhenJsonIsInvalid_ShouldSendToDeadLetterWithoutExecutingUseCase()
    {
        var consumer = new FakeRabbitMqNotificationConsumer();
        var useCase = new SpyRegisterNotificationRequestUseCase();
        await using var serviceProvider = CreateServiceProvider(useCase);
        var hostedService = CreateHostedService(serviceProvider, consumer);

        await hostedService.StartAsync(CancellationToken.None);

        var disposition = await consumer.ProcessAsync("{ invalid-json");

        Assert.Equal(RabbitMqNotificationDisposition.DeadLetter, disposition);
        Assert.Empty(useCase.Requests);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessMessage_WhenUseCaseReturnsDomainError_ShouldSendToDeadLetter()
    {
        var consumer = new FakeRabbitMqNotificationConsumer();
        var useCase = new SpyRegisterNotificationRequestUseCase
        {
            ExceptionToThrow = new DomainException("Canal de notificação inválido.")
        };
        await using var serviceProvider = CreateServiceProvider(useCase);
        var hostedService = CreateHostedService(serviceProvider, consumer);

        await hostedService.StartAsync(CancellationToken.None);

        var disposition = await consumer.ProcessAsync(JsonSerializer.Serialize(CreateRequest()));

        Assert.Equal(RabbitMqNotificationDisposition.DeadLetter, disposition);
        Assert.Single(useCase.Requests);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessMessage_WhenUseCaseReturnsTechnicalError_ShouldRequeue()
    {
        var consumer = new FakeRabbitMqNotificationConsumer();
        var useCase = new SpyRegisterNotificationRequestUseCase
        {
            ExceptionToThrow = new InvalidOperationException("Banco temporariamente indisponível com código 123456.")
        };
        await using var serviceProvider = CreateServiceProvider(useCase);
        var hostedService = CreateHostedService(serviceProvider, consumer);

        await hostedService.StartAsync(CancellationToken.None);

        var disposition = await consumer.ProcessAsync(JsonSerializer.Serialize(CreateRequest()));

        Assert.Equal(RabbitMqNotificationDisposition.Requeue, disposition);
        Assert.Single(useCase.Requests);

        await hostedService.StopAsync(CancellationToken.None);
    }

    private static ServiceProvider CreateServiceProvider(IRegisterNotificationRequestUseCase useCase)
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => useCase);

        return services.BuildServiceProvider();
    }

    private static RabbitMqNotificationConsumerHostedService CreateHostedService(
        ServiceProvider serviceProvider,
        IRabbitMqNotificationConsumer consumer)
    {
        return CreateHostedService(serviceProvider, consumer, new RabbitMqOptions());
    }

    private static RabbitMqNotificationConsumerHostedService CreateHostedService(
        ServiceProvider serviceProvider,
        IRabbitMqNotificationConsumer consumer,
        RabbitMqOptions options)
    {
        return new RabbitMqNotificationConsumerHostedService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            consumer,
            Options.Create(options),
            NullLogger<RabbitMqNotificationConsumerHostedService>.Instance);
    }

    private static SendTransactionalNotificationRequested CreateRequest()
    {
        return new SendTransactionalNotificationRequested
        {
            MessageId = Guid.Parse("0d4caa56-b276-46c8-98b5-4ab562206dea"),
            CorrelationId = "correlation-123",
            Source = "AuthCore",
            Channel = "Email",
            Recipient = "user@example.com",
            TemplateKey = "auth.email-confirmation",
            Variables = new Dictionary<string, string>
            {
                ["confirmationCode"] = "123456",
                ["expiresInMinutes"] = "15"
            },
            Priority = "High",
            IdempotencyKey = "auth-email-confirmation:5fbd8e65-3a5c-4789-8493-2706bb71f62b",
            RequestedAtUtc = new DateTime(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private sealed class FakeRabbitMqNotificationConsumer : IRabbitMqNotificationConsumer
    {
        /// <summary>
        /// Campo que armazena started.
        /// </summary>
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private Func<RabbitMqNotificationMessage, CancellationToken, Task<RabbitMqNotificationDisposition>>? _handler;

        public bool WasStarted { get; private set; }

        public async Task StartAsync(
            Func<RabbitMqNotificationMessage, CancellationToken, Task<RabbitMqNotificationDisposition>> handler,
            CancellationToken cancellationToken = default)
        {
            WasStarted = true;
            _handler = handler;
            _started.SetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        public async Task<RabbitMqNotificationDisposition> ProcessAsync(string body)
        {
            await _started.Task;

            var message = new RabbitMqNotificationMessage(
                Encoding.UTF8.GetBytes(body),
                "message-metadata-id",
                "correlation-metadata-id");

            return await _handler!(message, CancellationToken.None);
        }

        public void Dispose()
        {
        }
    }

    private sealed class SpyRegisterNotificationRequestUseCase : IRegisterNotificationRequestUseCase
    {
        public Exception? ExceptionToThrow { get; init; }

        public List<SendTransactionalNotificationRequested> Requests { get; } = [];

        public Task<RegisterNotificationRequestResult> Execute(RegisterNotificationRequestCommand command)
        {
            Requests.Add(command.Request);

            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Task.FromResult(new RegisterNotificationRequestResult
            {
                NotificationId = Guid.Parse("9ea1ef76-3491-4e7e-a6b8-1e2f6ee624dd"),
                WasCreated = true,
                WasDuplicate = false
            });
        }
    }
}
