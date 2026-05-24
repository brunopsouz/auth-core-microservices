using AuthCore.Domain.Common.DomainEvents;
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
using AuthCore.Infrastructure.Security.Emails;
using AuthCore.Infrastructure.Security.Cryptography;
using AuthCore.Infrastructure.Security.Tokens;
using AuthCore.Infrastructure.Services.Caching;
using AuthCore.Infrastructure.Services.Messaging;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddOptions(services, configuration);
        AddPersistence(services);
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
    private static void AddPersistence(IServiceCollection services)
    {
        services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<NpgsqlUnitOfWork>();
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<NpgsqlUnitOfWork>());
        services.AddScoped<IDatabaseSession>(serviceProvider => serviceProvider.GetRequiredService<NpgsqlUnitOfWork>());
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var redisOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
        });
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
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IEmailVerificationService, Sha256EmailVerificationService>();
        services.AddScoped<IEmailVerificationRequestedPublisher, EmailVerificationRequestedPublisher>();
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
        services.AddScoped<IUserRepository>(serviceProvider => serviceProvider.GetRequiredService<UserRepository>());
        services.AddScoped<IUserReadRepository>(serviceProvider => serviceProvider.GetRequiredService<UserReadRepository>());
        services.AddScoped<IPasswordRepository, PasswordRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxRepository>();
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

}
