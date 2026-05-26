using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationCore.Api.Workers;
using NotificationCore.Application.Notifications.UseCases.DispatchPendingNotification;
using NotificationCore.Infrastructure.Configurations;
using NotificationCore.Infrastructure.Observability;

namespace NotificationCore.IntegrationTests.Api;

public sealed class NotificationDispatcherHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDispatcherIsEnabled_ShouldExecuteUseCaseWithConfiguredCommand()
    {
        var useCase = new SpyDispatchPendingNotificationUseCase();
        await using var serviceProvider = CreateServiceProvider(useCase);
        var hostedService = CreateHostedService(serviceProvider, CreateEnabledOptions());

        await hostedService.StartAsync(CancellationToken.None);

        var command = await useCase.WaitForCommandAsync();

        Assert.Equal(5, command.Take);
        Assert.Equal(TimeSpan.FromSeconds(30), command.RetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(60), command.ProcessingTimeout);
        Assert.Equal(DateTimeKind.Utc, command.DueAtUtc.Kind);
        Assert.Equal(1, useCase.ExecutionCount);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDispatcherIsDisabled_ShouldNotExecuteUseCase()
    {
        var useCase = new SpyDispatchPendingNotificationUseCase();
        await using var serviceProvider = CreateServiceProvider(useCase);
        var hostedService = CreateHostedService(serviceProvider, new NotificationDispatcherOptions
        {
            Enabled = false
        });

        await hostedService.StartAsync(CancellationToken.None);

        Assert.Equal(0, useCase.ExecutionCount);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_WhenDispatcherIsDelayingNextCycle_ShouldCancelWithoutRunningSecondCycle()
    {
        var useCase = new SpyDispatchPendingNotificationUseCase();
        await using var serviceProvider = CreateServiceProvider(useCase);
        var hostedService = CreateHostedService(serviceProvider, CreateEnabledOptions());

        await hostedService.StartAsync(CancellationToken.None);
        await useCase.WaitForCommandAsync();
        await hostedService.StopAsync(CancellationToken.None);

        Assert.Equal(1, useCase.ExecutionCount);
    }

    private static ServiceProvider CreateServiceProvider(IDispatchPendingNotificationUseCase useCase)
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => useCase);

        return services.BuildServiceProvider();
    }

    private static NotificationDispatcherHostedService CreateHostedService(
        ServiceProvider serviceProvider,
        NotificationDispatcherOptions options)
    {
        return new NotificationDispatcherHostedService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            new NotificationMetrics(),
            NullLogger<NotificationDispatcherHostedService>.Instance);
    }

    private static NotificationDispatcherOptions CreateEnabledOptions()
    {
        return new NotificationDispatcherOptions
        {
            Enabled = true,
            BatchSize = 5,
            PollingIntervalSeconds = 300,
            RetryDelaySeconds = 30,
            ProcessingTimeoutSeconds = 60
        };
    }

    private sealed class SpyDispatchPendingNotificationUseCase : IDispatchPendingNotificationUseCase
    {
        /// <summary>
        /// Campo que armazena command received.
        /// </summary>
        private readonly TaskCompletionSource<DispatchPendingNotificationCommand> _commandReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ExecutionCount { get; private set; }

        public Task<DispatchPendingNotificationResult> Execute(DispatchPendingNotificationCommand command)
        {
            ExecutionCount++;
            _commandReceived.TrySetResult(command);

            return Task.FromResult(new DispatchPendingNotificationResult
            {
                Found = 1,
                Sent = 1,
                RetryScheduled = 0,
                DeadLettered = 0
            });
        }

        public async Task<DispatchPendingNotificationCommand> WaitForCommandAsync()
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            return await _commandReceived.Task.WaitAsync(cancellationTokenSource.Token);
        }
    }
}
