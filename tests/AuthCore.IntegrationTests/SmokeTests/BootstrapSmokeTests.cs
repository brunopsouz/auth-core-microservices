using AuthCore.Api;
using AuthCore.Api.Controllers;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Application;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Infrastructure;
using AuthCore.Infrastructure.Configurations;
using AuthCore.Infrastructure.Services.Messaging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AuthCore.IntegrationTests.SmokeTests;

public sealed class BootstrapSmokeTests
{
    [Fact]
    public async Task Build_WhenApiDependenciesAreRegistered_ShouldCreateServiceProvider()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSql"] = "Host=localhost;Port=5432;Database=auth_core_tests;Username=postgres;Password=postgres",
            ["Database:Migrations:AutoMigrateOnStartup"] = "false",
            ["Database:Migrations:EnsureDatabaseCreated"] = "false",
            ["Database:Migrations:AdminDatabase"] = "postgres",
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = "12345678901234567890123456789012",
            ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = "15",
            ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "authcore-tests",
            ["RabbitMq:Host"] = "localhost",
            ["RabbitMq:Port"] = "5672",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:Username"] = "guest",
            ["RabbitMq:Password"] = "guest",
            ["RabbitMq:Exchange"] = "notification.requests",
            ["RabbitMq:RoutingKey"] = "notification.email.requested",
            ["RabbitMq:Queue"] = "notification.email.requests",
            ["RabbitMq:DeadLetterQueue"] = "notification.email.requests.dlq",
            ["Outbox:Enabled"] = "false",
            ["Outbox:BatchSize"] = "20",
            ["Outbox:PollingIntervalSeconds"] = "10",
            ["Outbox:MaxAttempts"] = "5"
        });

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(UserController).Assembly);

        builder.Services.AddApi(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApplication();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();

        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
        var accessTokenGenerator = scope.ServiceProvider.GetService<IAccessTokenGenerator>();
        var refreshTokenService = scope.ServiceProvider.GetService<IRefreshTokenService>();
        var authenticationSchemeProvider = scope.ServiceProvider.GetService<IAuthenticationSchemeProvider>();
        var healthCheckService = scope.ServiceProvider.GetService<HealthCheckService>();
        var outboxProcessor = scope.ServiceProvider.GetService<IOutboxProcessor>();
        var notificationRequestPublisher = scope.ServiceProvider.GetService<INotificationRequestPublisher>();
        var hostedServices = app.Services.GetServices<IHostedService>();

        Assert.Equal("authcore-tests", jwtOptions.Issuer);
        Assert.Equal(7, jwtOptions.RefreshTokenLifetimeDays);
        Assert.Equal(60, jwtOptions.ClockSkewSeconds);
        Assert.NotNull(unitOfWork);
        Assert.NotNull(accessTokenGenerator);
        Assert.NotNull(refreshTokenService);
        Assert.NotNull(authenticationSchemeProvider);
        Assert.NotNull(healthCheckService);
        Assert.NotNull(outboxProcessor);
        Assert.NotNull(notificationRequestPublisher);
        Assert.Contains(hostedServices, service => service.GetType().Name == "OutboxHostedService");
    }
}
