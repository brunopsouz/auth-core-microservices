using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Providers;
using NotificationCore.Domain.Notifications.Repositories;
using NotificationCore.Domain.Notifications.Rendering;
using NotificationCore.Domain.Notifications.Templates;
using NotificationCore.Infrastructure.Abstractions.Data;
using NotificationCore.Infrastructure.Configurations;
using NotificationCore.Infrastructure.Messaging.RabbitMq;
using NotificationCore.Infrastructure.Notifications.Providers;
using NotificationCore.Infrastructure.Notifications.Rendering;
using NotificationCore.Infrastructure.Notifications.Templates;
using NotificationCore.Infrastructure.Observability;
using NotificationCore.Infrastructure.Persistences.Migrations.Versions;
using NotificationCore.Infrastructure.Persistences.Read.PostgreSQL.Repositories;
using NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.Connections;
using NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;
using NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.UnitOfWork;

namespace NotificationCore.Infrastructure;

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
        AddRepositories(services);
        AddRendering(services);
        AddProviders(services);
        AddMessaging(services);
        AddObservability(services);
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
        AddRabbitMqOptions(services, configuration);
        AddSmtpOptions(services, configuration);
        AddNotificationDispatcherOptions(services, configuration);
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
    }

    /// <summary>
    /// Operação para adicionar os repositórios da infraestrutura.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<IInboxRepository, InboxRepository>();
        services.AddScoped<NotificationRepository>();
        services.AddScoped<INotificationWriterRepository>(serviceProvider => serviceProvider.GetRequiredService<NotificationRepository>());
        services.AddScoped<INotificationReadRepository>(serviceProvider => serviceProvider.GetRequiredService<NotificationRepository>());
        services.AddScoped<INotificationSearchRepository>(serviceProvider => serviceProvider.GetRequiredService<NotificationRepository>());
        services.AddScoped<INotificationDispatchRepository>(serviceProvider => serviceProvider.GetRequiredService<NotificationRepository>());
        services.AddScoped<NotificationTemplateRepository>();
        services.AddScoped<INotificationTemplateRepository>(serviceProvider => serviceProvider.GetRequiredService<NotificationTemplateRepository>());
        services.AddScoped<INotificationTemplateReadRepository>(serviceProvider => serviceProvider.GetRequiredService<NotificationTemplateRepository>());
    }

    /// <summary>
    /// Operação para adicionar serviços de renderização de templates.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddRendering(IServiceCollection services)
    {
        services.AddScoped<ITemplateRenderer, SimpleTemplateRenderer>();
    }

    /// <summary>
    /// Operação para adicionar provedores externos de notificação.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddProviders(IServiceCollection services)
    {
        services.AddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
        services.AddScoped<IEmailProvider>(serviceProvider => new SmtpEmailProvider(
            serviceProvider.GetRequiredService<IOptions<SmtpOptions>>(),
            serviceProvider.GetRequiredService<ISmtpClientFactory>(),
            serviceProvider.GetRequiredService<NotificationMetrics>(),
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SmtpEmailProvider>>()));
    }

    /// <summary>
    /// Operação para adicionar serviços de mensageria.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddMessaging(IServiceCollection services)
    {
        services.AddSingleton<IRabbitMqNotificationConsumer, RabbitMqNotificationConsumer>();
    }

    /// <summary>
    /// Operação para adicionar serviços de observabilidade.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddObservability(IServiceCollection services)
    {
        services.AddSingleton<NotificationMetrics>();
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
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
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
            .ValidateDataAnnotations()
            .ValidateOnStart();
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
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    /// <summary>
    /// Operação para adicionar as opções de SMTP.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddSmtpOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    /// <summary>
    /// Operação para adicionar as opções do despachante de notificações.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddNotificationDispatcherOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<NotificationDispatcherOptions>()
            .Bind(configuration.GetSection(NotificationDispatcherOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
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
