using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using AuthCore.Api;
using AuthCore.Api.Authentication;
using AuthCore.Api.Controllers;
using AuthCore.Application;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure;
using AuthCore.Infrastructure.Abstractions.Data;
using AuthCore.Infrastructure.Configurations;
using AuthCore.Infrastructure.Services.Messaging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shared.Observability;

namespace AuthCore.IntegrationTests.SmokeTests;

public sealed class BootstrapSmokeTests
{
    [Fact]
    public async Task Build_WhenApiDependenciesAreRegistered_ShouldCreateServiceProvider()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.Configuration.AddInMemoryCollection(CreateConfigurationValues());

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(UserController).Assembly);

        builder.Services.AddApi(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
        builder.Services.AddApplication();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();

        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
        var accessTokenGenerator = scope.ServiceProvider.GetService<IAccessTokenGenerator>();
        var refreshTokenService = scope.ServiceProvider.GetService<IRefreshTokenService>();
        var externalLoginRepository = scope.ServiceProvider.GetService<IExternalLoginRepository>();
        var externalLoginReadRepository = scope.ServiceProvider.GetService<IExternalLoginReadRepository>();
        var authenticationSchemeProvider = scope.ServiceProvider.GetService<IAuthenticationSchemeProvider>();
        var googleAuthenticationOptions = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get("Google");
        var healthCheckService = scope.ServiceProvider.GetService<HealthCheckService>();
        var healthCheckOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value;
        var outboxProcessor = scope.ServiceProvider.GetService<IOutboxProcessor>();
        var notificationRequestPublisher = scope.ServiceProvider.GetService<INotificationRequestPublisher>();
        var dataProtectionProvider = scope.ServiceProvider.GetService<IDataProtectionProvider>();
        var dataProtectionOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<DataProtectionKeyRingOptions>>()
            .Value;
        var keyManagementOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value;
        var hostedServices = app.Services.GetServices<IHostedService>();

        Assert.Equal("authcore-tests", jwtOptions.Issuer);
        Assert.Equal(7, jwtOptions.RefreshTokenLifetimeDays);
        Assert.Equal(60, jwtOptions.ClockSkewSeconds);
        Assert.NotNull(unitOfWork);
        Assert.NotNull(accessTokenGenerator);
        Assert.NotNull(refreshTokenService);
        Assert.NotNull(externalLoginRepository);
        Assert.NotNull(externalLoginReadRepository);
        Assert.NotNull(authenticationSchemeProvider);
        Assert.False(googleAuthenticationOptions.SaveTokens);
        Assert.Equal("AuthCore.External", googleAuthenticationOptions.SignInScheme);
        Assert.Equal("/api/auth/external/google/callback", googleAuthenticationOptions.CallbackPath);
        Assert.Contains("openid", googleAuthenticationOptions.Scope);
        Assert.Contains("profile", googleAuthenticationOptions.Scope);
        Assert.Contains("email", googleAuthenticationOptions.Scope);
        var emailVerifiedClaimActions = googleAuthenticationOptions.ClaimActions
            .OfType<JsonKeyClaimAction>()
            .Where(action => action.ClaimType == "urn:google:email_verified")
            .ToArray();
        Assert.Collection(
            emailVerifiedClaimActions.OrderBy(action => action.JsonKey),
            action => AssertGoogleEmailVerifiedClaimAction(action, "email_verified"),
            action => AssertGoogleEmailVerifiedClaimAction(action, "verified_email"));
        AssertGoogleEmailVerifiedClaimIsMapped(
            googleAuthenticationOptions,
            """{"email_verified":true}""");
        AssertGoogleEmailVerifiedClaimIsMapped(
            googleAuthenticationOptions,
            """{"verified_email":true}""");
        Assert.NotNull(healthCheckService);
        AssertHealthCheck(healthCheckOptions, "self", "live", "ready");
        AssertHealthCheck(healthCheckOptions, "postgresql", "ready", "dependency", "critical");
        AssertHealthCheck(healthCheckOptions, "redis", "ready", "dependency", "critical");
        Assert.DoesNotContain(healthCheckOptions.Registrations, registration => registration.Name == "rabbitmq");
        Assert.NotNull(outboxProcessor);
        Assert.NotNull(notificationRequestPublisher);
        Assert.NotNull(dataProtectionProvider);
        Assert.Equal("AuthCore", dataProtectionOptions.ApplicationName);
        Assert.Equal("data-protection-keys", dataProtectionOptions.KeyName);
        Assert.False(dataProtectionOptions.RequireCertificate);
        Assert.Equal(
            "RedisXmlRepository",
            keyManagementOptions.XmlRepository?.GetType().Name);
        Assert.Contains(hostedServices, service => service.GetType().Name == "OutboxHostedService");
    }

    private static void AssertHealthCheck(
        HealthCheckServiceOptions options,
        string name,
        params string[] tags)
    {
        var registration = Assert.Single(options.Registrations.Where(candidate => candidate.Name == name));

        foreach (var tag in tags)
            Assert.Contains(tag, registration.Tags);
    }

    [Fact]
    public void AddApi_WhenOutboxIsEnabled_ShouldRegisterRabbitMqAsOptionalDependency()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(CreateConfigurationValues(outboxEnabled: "true"));
        builder.Services.AddApi(builder.Configuration);

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var healthCheckOptions = serviceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value;

        AssertHealthCheck(healthCheckOptions, "rabbitmq", "dependency", "optional");
    }

    [Fact]
    public void AddInfrastructure_WhenDataProtectionCertificateIsRequired_ShouldRejectMissingCertificate()
    {
        var configurationValues = CreateConfigurationValues();
        configurationValues["DataProtection:RequireCertificate"] = "true";
        configurationValues["DataProtection:CertificatePath"] = string.Empty;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration));

        Assert.Equal(
            "O certificado de protecao das chaves do Data Protection e obrigatorio.",
            exception.Message);
    }

    [Fact]
    public void AddInfrastructure_WhenEnvironmentIsProduction_ShouldRequireCertificateProtection()
    {
        var configurationValues = CreateConfigurationValues();
        configurationValues["DataProtection:RequireCertificate"] = "false";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(configurationValues);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Services.AddInfrastructure(
                builder.Configuration,
                builder.Environment));

        Assert.Equal(
            "A protecao das chaves do Data Protection por certificado e obrigatoria em Production.",
            exception.Message);
    }

    [Fact]
    public void AddInfrastructure_WhenProductionCertificateIsConfigured_ShouldProtectKeyRing()
    {
        const string certificatePassword = "AuthCore-Tests-Certificate-2026!";
        var certificatePath = Path.Combine(
            Path.GetTempPath(),
            $"authcore-data-protection-{Guid.NewGuid():N}.pfx");

        try
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=AuthCore Integration Tests",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddDays(1));

            File.WriteAllBytes(
                certificatePath,
                certificate.Export(X509ContentType.Pfx, certificatePassword));

            var configurationValues = CreateConfigurationValues();
            configurationValues["ASPNETCORE_ENVIRONMENT"] = Environments.Production;
            configurationValues["DataProtection:RequireCertificate"] = "true";
            configurationValues["DataProtection:CertificatePath"] = certificatePath;
            configurationValues["DataProtection:CertificatePassword"] = certificatePassword;
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues)
                .Build();
            var services = new ServiceCollection();

            services.AddInfrastructure(configuration);

            using var serviceProvider = services.BuildServiceProvider();
            var keyManagementOptions = serviceProvider
                .GetRequiredService<IOptions<KeyManagementOptions>>()
                .Value;

            Assert.Equal(
                "CertificateXmlEncryptor",
                keyManagementOptions.XmlEncryptor?.GetType().Name);
        }
        finally
        {
            if (File.Exists(certificatePath))
                File.Delete(certificatePath);
        }
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
        builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
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
        app.MapStandardHealthCheckEndpoints();
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

    private static Dictionary<string, string?> CreateConfigurationValues(string outboxEnabled = "false")
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
            ["Authentication:Google:ClientId"] = "google-client-id",
            ["Authentication:Google:ClientSecret"] = "google-client-secret",
            ["Authentication:Google:CallbackPath"] = "/api/auth/external/google/callback",
            ["Authentication:DefaultReturnUrl"] = "http://localhost:5173",
            ["Authentication:AllowedReturnUrls:0"] = "http://localhost:5173",
            ["Auth:Csrf:SigningKey"] = "tests-csrf-signing-key-2026",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["Redis:KeyPrefix"] = "authcore-tests",
            ["DataProtection:ApplicationName"] = "AuthCore",
            ["DataProtection:KeyName"] = "data-protection-keys",
            ["DataProtection:RequireCertificate"] = "false",
            ["RabbitMq:Host"] = "localhost",
            ["RabbitMq:Port"] = "5672",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:Username"] = "guest",
            ["RabbitMq:Password"] = "guest",
            ["RabbitMq:Exchange"] = "notification.requests",
            ["RabbitMq:RoutingKey"] = "notification.email.requested",
            ["RabbitMq:Queue"] = "notification.email.requests",
            ["RabbitMq:DeadLetterQueue"] = "notification.email.requests.dlq",
            ["Outbox:Enabled"] = outboxEnabled,
            ["Outbox:BatchSize"] = "20",
            ["Outbox:PollingIntervalSeconds"] = "10",
            ["Outbox:MaxAttempts"] = "5"
        };
    }

    private static void AssertGoogleEmailVerifiedClaimAction(
        JsonKeyClaimAction action,
        string expectedJsonKey)
    {
        Assert.Equal(expectedJsonKey, action.JsonKey);
        Assert.Equal(ClaimValueTypes.Boolean, action.ValueType);
    }

    private static void AssertGoogleEmailVerifiedClaimIsMapped(
        GoogleOptions options,
        string userInformationJson)
    {
        using var userInformation = JsonDocument.Parse(userInformationJson);
        var identity = new ClaimsIdentity();

        foreach (var claimAction in options.ClaimActions)
        {
            claimAction.Run(
                userInformation.RootElement,
                identity,
                ExternalAuthenticationDefaults.GoogleScheme);
        }

        var claimValue = identity.FindFirst("urn:google:email_verified")?.Value;

        Assert.True(bool.TryParse(claimValue, out var emailVerified));
        Assert.True(emailVerified);
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
