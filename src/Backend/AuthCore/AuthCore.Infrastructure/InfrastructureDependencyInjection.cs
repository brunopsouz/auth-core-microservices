using System.Security.Cryptography.X509Certificates;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using AuthCore.Infrastructure.Configurations;
using AuthCore.Infrastructure.Persistences.Migrations.Versions;
using AuthCore.Infrastructure.Persistences.Read.PostgreSQL.Repositories;
using AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Connections;
using AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;
using AuthCore.Infrastructure.Persistences.Write.PostgreSQL.UnitOfWork;
using AuthCore.Infrastructure.Observability;
using AuthCore.Infrastructure.Security.Emails;
using AuthCore.Infrastructure.Security.Cryptography;
using AuthCore.Infrastructure.Security.Tokens;
using AuthCore.Infrastructure.Services.Caching;
using AuthCore.Infrastructure.Services.Messaging;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;

namespace AuthCore.Infrastructure;

/// <summary>
/// Define operações para registrar dependências da infraestrutura.
/// </summary>
public static class InfrastructureDependencyInjection
{
    /// <summary>
    /// Operação para adicionar os serviços de infraestrutura.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>Coleção de serviços atualizada.</returns>
    /// <param name="hostEnvironment">Ambiente atual da aplicacao.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? hostEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddOptions(services, configuration);
        AddPersistence(services, configuration, hostEnvironment);
        AddSecurity(services);
        AddRepositories(services);
        AddMigrations(services, configuration);

        return services;
    }

    /// <summary>
    /// Operação para adicionar as opções de configuração da infraestrutura.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        AddDatabaseOptions(services, configuration);
        AddDatabaseMigrationOptions(services, configuration);
        AddJwtOptions(services, configuration);
        AddRedisOptions(services, configuration);
        AddDataProtectionOptions(services, configuration);
        AddRabbitMqOptions(services, configuration);
        AddSessionOptions(services, configuration);
        AddCookieOptions(services, configuration);
        AddCsrfOptions(services, configuration);
        AddLoginRateLimitOptions(services, configuration);
        AddEmailVerificationOptions(services, configuration);
        AddOutboxOptions(services, configuration);
    }

    /// <summary>
    /// Operação para adicionar as dependências de persistência.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddPersistence(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? hostEnvironment)
    {
        var redisOptions = GetRedisOptions(configuration);
        var connectionMultiplexer = new Lazy<IConnectionMultiplexer>(
            () => ConnectionMultiplexer.Connect(redisOptions.ConnectionString),
            LazyThreadSafetyMode.ExecutionAndPublication);

        services.AddSingleton<DatabaseMetrics>();
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return NpgsqlDataSource.Create(BuildConnectionString(options.PostgreSql, "AuthCore"));
        });
        services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<NpgsqlUnitOfWork>();
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<NpgsqlUnitOfWork>());
        services.AddScoped<IDatabaseSession>(serviceProvider => serviceProvider.GetRequiredService<NpgsqlUnitOfWork>());
        services.AddSingleton<IConnectionMultiplexer>(_ => connectionMultiplexer.Value);

        AddDataProtection(
            services,
            configuration,
            redisOptions,
            connectionMultiplexer,
            hostEnvironment);
    }

    /// <summary>
    /// Operacao para configurar o key ring compartilhado do Data Protection.
    /// </summary>
    private static void AddDataProtection(
        IServiceCollection services,
        IConfiguration configuration,
        RedisOptions redisOptions,
        Lazy<IConnectionMultiplexer> connectionMultiplexer,
        IHostEnvironment? hostEnvironment)
    {
        var options = GetDataProtectionOptions(configuration);
        var environmentName = hostEnvironment?.EnvironmentName
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"];

        if (string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase)
            && !options.RequireCertificate)
        {
            throw new InvalidOperationException(
                "A protecao das chaves do Data Protection por certificado e obrigatoria em Production.");
        }

        var redisKey = $"{redisOptions.KeyPrefix.Trim()}:{options.KeyName.Trim()}";
        var dataProtectionBuilder = services
            .AddDataProtection()
            .SetApplicationName(options.ApplicationName.Trim())
            .PersistKeysToStackExchangeRedis(
                () => connectionMultiplexer.Value.GetDatabase(),
                redisKey);

        var certificatePath = options.CertificatePath?.Trim();

        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            if (options.RequireCertificate)
            {
                throw new InvalidOperationException(
                    "O certificado de protecao das chaves do Data Protection e obrigatorio.");
            }

            return;
        }

        if (!File.Exists(certificatePath))
        {
            throw new InvalidOperationException(
                $"O certificado do Data Protection nao foi encontrado em '{certificatePath}'.");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            certificatePath,
            options.CertificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);

        dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
    }

    /// <summary>
    /// Operação para adicionar os serviços de segurança.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddSecurity(IServiceCollection services)
    {
        services.AddScoped<IPasswordEncripter, BCryptNet>();
        services.AddScoped<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ISessionIdentifierHasher, Sha256SessionIdentifierHasher>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IEmailVerificationService, Sha256EmailVerificationService>();
        services.AddScoped<IEmailVerificationNotificationOutboxFactory, EmailVerificationNotificationOutboxFactory>();
        services.AddScoped<INotificationRequestPublisher, RabbitMqNotificationRequestPublisher>();
        services.AddSingleton<OutboxMetrics>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
    }

    /// <summary>
    /// Operação para adicionar os repositórios da infraestrutura.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<UserRepository>();
        services.AddScoped<UserReadRepository>();
        services.AddScoped<ExternalLoginRepository>();
        services.AddScoped<ExternalLoginReadRepository>();
        services.AddScoped<IUserRepository>(serviceProvider => serviceProvider.GetRequiredService<UserRepository>());
        services.AddScoped<IUserReadRepository>(serviceProvider => serviceProvider.GetRequiredService<UserReadRepository>());
        services.AddScoped<IExternalLoginRepository>(serviceProvider => serviceProvider.GetRequiredService<ExternalLoginRepository>());
        services.AddScoped<IExternalLoginReadRepository>(serviceProvider => serviceProvider.GetRequiredService<ExternalLoginReadRepository>());
        services.AddScoped<IPasswordRepository, PasswordRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDurableSessionRepository, DurableSessionRepository>();
        services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<ISessionStore, RedisSessionStore>();
    }

    /// <summary>
    /// Operação para adicionar o FluentMigrator.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddMigrations(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetPostgreSqlConnectionString(configuration);

        services
            .AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(DatabaseVersions).Assembly).For.Migrations());
    }

    /// <summary>
    /// Operação para adicionar as opções de banco de dados.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddDatabaseOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));
    }

    /// <summary>
    /// Operação para adicionar as opções de migração do banco.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddDatabaseMigrationOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseMigrationOptions>()
            .Bind(configuration.GetSection(DatabaseMigrationOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operação para adicionar as opções de JWT.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddJwtOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operação para adicionar as opções de Redis.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddRedisOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operacao para adicionar as opcoes do Data Protection.
    /// </summary>
    private static void AddDataProtectionOptions(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DataProtectionKeyRingOptions>()
            .Bind(configuration.GetSection(DataProtectionKeyRingOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operação para adicionar as opções de RabbitMQ.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddRabbitMqOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operação para adicionar as opções de sessão.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddSessionOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SessionOptions>()
            .Bind(configuration.GetSection(SessionOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operação para adicionar as opções do cookie.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddCookieOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuthCookieOptions>()
            .Bind(configuration.GetSection(AuthCookieOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operação para adicionar as opções de CSRF.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddCsrfOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CsrfOptions>()
            .Bind(configuration.GetSection(CsrfOptions.SectionName));
    }

    /// <summary>
    /// Operação para adicionar as opções de limitação de login.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddLoginRateLimitOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<LoginRateLimitOptions>()
            .Bind(configuration.GetSection(LoginRateLimitOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operação para adicionar as opções de verificação de e-mail.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddEmailVerificationOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<EmailVerificationOptions>()
            .Bind(configuration.GetSection(EmailVerificationOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operação para adicionar as opções da outbox.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddOutboxOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .ValidateDataAnnotations();
    }

    /// <summary>
    /// Operação para obter a connection string do PostgreSQL.
    /// </summary>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>Connection string configurada para o PostgreSQL.</returns>
    private static string GetPostgreSqlConnectionString(IConfiguration configuration)
    {
        return configuration.GetConnectionString("PostgreSql")
            ?? configuration.GetSection(DatabaseOptions.SectionName).GetValue<string>(nameof(DatabaseOptions.PostgreSql))
            ?? string.Empty;
    }

    private static RedisOptions GetRedisOptions(IConfiguration configuration)
    {
        var options = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>()
            ?? new RedisOptions();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new InvalidOperationException("A connection string do Redis nao foi configurada.");

        if (string.IsNullOrWhiteSpace(options.KeyPrefix))
            throw new InvalidOperationException("O prefixo das chaves Redis nao foi configurado.");

        return options;
    }

    private static DataProtectionKeyRingOptions GetDataProtectionOptions(
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(DataProtectionKeyRingOptions.SectionName)
            .Get<DataProtectionKeyRingOptions>()
            ?? new DataProtectionKeyRingOptions();

        if (string.IsNullOrWhiteSpace(options.ApplicationName))
            throw new InvalidOperationException("O nome da aplicacao do Data Protection nao foi configurado.");

        if (string.IsNullOrWhiteSpace(options.KeyName))
            throw new InvalidOperationException("O nome da chave Redis do Data Protection nao foi configurado.");

        return options;
    }

    private static string BuildConnectionString(string connectionString, string applicationName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Database connection string was not configured.");

        var configured = new NpgsqlConnectionStringBuilder(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = configured.ContainsKey("Pooling")
                ? configured.Pooling
                : true,
            MinPoolSize = configured.ContainsKey("Minimum Pool Size")
                ? configured.MinPoolSize
                : 0,
            MaxPoolSize = configured.ContainsKey("Maximum Pool Size")
                || configured.ContainsKey("Max Pool Size")
                    ? configured.MaxPoolSize
                    : 30,
            Timeout = configured.ContainsKey("Timeout")
                ? configured.Timeout
                : 10,
            CommandTimeout = configured.ContainsKey("Command Timeout")
                ? configured.CommandTimeout
                : 30,
            ApplicationName = string.IsNullOrWhiteSpace(configured.ApplicationName)
                ? applicationName
                : configured.ApplicationName
        };

        return builder.ConnectionString;
    }

}
