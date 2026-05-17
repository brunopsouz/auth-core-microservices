using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NotificationCore.Api;
using NotificationCore.Api.Workers;
using NotificationCore.Application;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Providers;
using NotificationCore.Domain.Notifications.Rendering;
using NotificationCore.Domain.Notifications.Repositories;
using NotificationCore.Infrastructure;
using NotificationCore.Infrastructure.Abstractions.Data;
using NotificationCore.Infrastructure.Configurations;
using NotificationCore.Infrastructure.Messaging.RabbitMq;
using NotificationCore.Infrastructure.Notifications.Providers;
using NotificationCore.Infrastructure.Notifications.Rendering;
using NotificationCore.Infrastructure.Notifications.Templates;
using NotificationCore.Infrastructure.Observability;
using NotificationCore.Infrastructure.Persistences.Read.PostgreSQL.Repositories;
using NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;
using NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.UnitOfWork;

namespace NotificationCore.IntegrationTests.SmokeTests;

public sealed class BootstrapSmokeTests
{
    [Fact]
    public async Task AddInfrastructure_WhenCompositionIsBuilt_ShouldResolveInfrastructureServices()
    {
        var configuration = CreateInfrastructureConfiguration();

        await using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        await using var scope = serviceProvider.CreateAsyncScope();

        Assert.IsType<NpgsqlUnitOfWork>(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.Same(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            scope.ServiceProvider.GetRequiredService<IDatabaseSession>());
        Assert.IsType<InboxRepository>(scope.ServiceProvider.GetRequiredService<IInboxRepository>());
        Assert.IsType<NotificationRepository>(scope.ServiceProvider.GetRequiredService<INotificationRepository>());
        Assert.IsType<NotificationTemplateRepository>(scope.ServiceProvider.GetRequiredService<INotificationTemplateRepository>());
        Assert.IsType<SimpleTemplateRenderer>(scope.ServiceProvider.GetRequiredService<ITemplateRenderer>());
        Assert.IsType<MailKitSmtpClientFactory>(scope.ServiceProvider.GetRequiredService<ISmtpClientFactory>());
        Assert.IsType<SmtpEmailProvider>(scope.ServiceProvider.GetRequiredService<IEmailProvider>());
        Assert.IsType<RabbitMqNotificationConsumer>(scope.ServiceProvider.GetRequiredService<IRabbitMqNotificationConsumer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<NotificationMetrics>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMigrationRunner>());
    }

    [Fact]
    public async Task Build_WhenApiInfrastructureAndApplicationAreRegistered_ShouldValidateComposition()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });
        builder.Configuration.AddConfiguration(CreateInfrastructureConfiguration());
        builder.Services.AddApi(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApplication();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();

        Assert.NotEmpty(app.Services.GetServices<IHostedService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmailProvider>());
    }

    [Fact]
    public async Task Build_WhenInfrastructureConfigurationIsRegistered_ShouldBindOptions()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddConfiguration(CreateInfrastructureConfiguration());

        builder.Services.AddApi(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();

        var databaseOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var migrationOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseMigrationOptions>>().Value;
        var rabbitMqOptions = scope.ServiceProvider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        var smtpOptions = scope.ServiceProvider.GetRequiredService<IOptions<SmtpOptions>>().Value;
        var dispatcherOptions = scope.ServiceProvider.GetRequiredService<IOptions<NotificationDispatcherOptions>>().Value;
        var emailProvider = scope.ServiceProvider.GetRequiredService<IEmailProvider>();
        var rabbitMqConsumer = scope.ServiceProvider.GetRequiredService<IRabbitMqNotificationConsumer>();
        var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();
        var healthCheckOptions = scope.ServiceProvider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        Assert.Contains("notification_core_tests", databaseOptions.PostgreSql);
        Assert.False(migrationOptions.AutoMigrateOnStartup);
        Assert.True(migrationOptions.EnsureDatabaseCreated);
        Assert.Equal("postgres", migrationOptions.AdminDatabase);
        Assert.Equal(5672, rabbitMqOptions.Port);
        Assert.Equal("/", rabbitMqOptions.VirtualHost);
        Assert.Equal("notification.requests", rabbitMqOptions.Exchange);
        Assert.Equal("notification.email.requested", rabbitMqOptions.RoutingKey);
        Assert.Equal("notification.email.requests", rabbitMqOptions.Queue);
        Assert.Equal("notification.email.requests.dlq", rabbitMqOptions.DeadLetterQueue);
        Assert.Equal(587, smtpOptions.Port);
        Assert.Equal("notifications@example.com", smtpOptions.SenderEmail);
        Assert.True(smtpOptions.UseTls);
        Assert.Equal("NotificationCore", smtpOptions.SenderName);
        Assert.Equal(30, smtpOptions.TimeoutSeconds);
        Assert.True(dispatcherOptions.Enabled);
        Assert.Equal(20, dispatcherOptions.BatchSize);
        Assert.Equal(10, dispatcherOptions.PollingIntervalSeconds);
        Assert.Equal(300, dispatcherOptions.RetryDelaySeconds);
        Assert.Equal(900, dispatcherOptions.ProcessingTimeoutSeconds);
        Assert.True(rabbitMqOptions.Enabled);
        Assert.IsType<SmtpEmailProvider>(emailProvider);
        Assert.IsType<RabbitMqNotificationConsumer>(rabbitMqConsumer);
        Assert.Contains(hostedServices, hostedService => hostedService is RabbitMqNotificationConsumerHostedService);
        Assert.Contains(hostedServices, hostedService => hostedService is NotificationDispatcherHostedService);
        Assert.Contains(healthCheckOptions.Registrations, registration => registration.Name == "postgresql");
        Assert.Contains(healthCheckOptions.Registrations, registration => registration.Name == "rabbitmq");
        Assert.Contains(healthCheckOptions.Registrations, registration => registration.Name == "dispatcher");
    }

    [Fact]
    public async Task Health_WhenApiIsStarted_ShouldReturnOk()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSql"] = "Host=localhost;Port=5432;Database=notification_core_tests;Username=postgres;Password=postgres",
            ["Database:Migrations:AutoMigrateOnStartup"] = "false",
            ["RabbitMq:Enabled"] = "false",
            ["RabbitMq:Host"] = "localhost",
            ["RabbitMq:Username"] = "guest",
            ["RabbitMq:Password"] = "guest",
            ["NotificationDispatcher:Enabled"] = "false",
            ["Smtp:Host"] = "localhost",
            ["Smtp:SenderEmail"] = "notifications@example.com"
        });

        builder.Services.AddApi(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApplication();
        builder.Services.AddScoped<IDbConnectionFactory, FakeDbConnectionFactory>();

        await using var app = builder.Build();

        app.UseExceptionHandler();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            AllowCachingResponses = false,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            }
        });
        app.MapControllers();
        app.Urls.Add("http://127.0.0.1:0");

        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();

        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync($"{address}/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await app.StopAsync();
    }

    private sealed class FakeDbConnectionFactory : IDbConnectionFactory
    {
        public Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IDbConnection>(new FakeDbConnection());
        }
    }

    private sealed class FakeDbConnection : IDbConnection
    {
        private string _connectionString = string.Empty;

        [AllowNull]
        public string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value ?? string.Empty;
        }

        public int ConnectionTimeout => 0;

        public string Database => "notification_core_tests";

        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction()
        {
            throw new NotSupportedException();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            throw new NotSupportedException();
        }

        public void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException();
        }

        public void Close()
        {
        }

        public IDbCommand CreateCommand()
        {
            throw new NotSupportedException();
        }

        public void Open()
        {
        }

        public void Dispose()
        {
        }
    }

    private static IConfiguration CreateInfrastructureConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = "Host=localhost;Port=5432;Database=notification_core_tests;Username=postgres;Password=postgres",
                ["RabbitMq:Host"] = "localhost",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["Smtp:Host"] = "localhost",
                ["Smtp:SenderEmail"] = "notifications@example.com"
            })
            .Build();
    }
}
