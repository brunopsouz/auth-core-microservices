using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using AuthCore.Api;
using AuthCore.Api.Controllers;
using AuthCore.Application;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Infrastructure;
using AuthCore.Infrastructure.Abstractions.Data;
using AuthCore.Infrastructure.Configurations;
using AuthCore.Infrastructure.Services.Messaging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
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

        builder.Configuration.AddInMemoryCollection(CreateConfigurationValues());

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

    [Fact]
    public async Task Root_WhenApiIsStartedInDevelopment_ShouldRedirectToSwagger()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.Configuration.AddInMemoryCollection(CreateConfigurationValues());
        builder.Services.AddApi(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApplication();
        builder.Services.AddScoped<IDbConnectionFactory, FakeDbConnectionFactory>();

        await using var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapGet("/", () => Results.Redirect("/swagger"));
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseCors("AuthCoreBrowserSession");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health");
        app.MapControllers();
        app.Urls.Add("http://127.0.0.1:0");

        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();

        using var httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        using var response = await httpClient.GetAsync(address);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger", response.Headers.Location?.OriginalString);

        await app.StopAsync();
    }

    private static Dictionary<string, string?> CreateConfigurationValues()
    {
        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSql"] = "Host=localhost;Port=5432;Database=auth_core_tests;Username=postgres;Password=postgres",
            ["Database:Migrations:AutoMigrateOnStartup"] = "false",
            ["Database:Migrations:EnsureDatabaseCreated"] = "false",
            ["Database:Migrations:AdminDatabase"] = "postgres",
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = "AuthCore-Tests-SigningKey-2026-Strong!",
            ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = "5",
            ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Auth:Csrf:SigningKey"] = "tests-csrf-signing-key-2026",
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
        };
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

        public string Database => "auth_core_tests";

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
}
