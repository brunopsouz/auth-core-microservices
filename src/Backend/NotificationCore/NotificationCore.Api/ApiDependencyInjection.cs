using NotificationCore.Api.Exceptions;
using NotificationCore.Api.HealthChecks;
using NotificationCore.Api.Observability;
using NotificationCore.Api.Workers;
using NotificationCore.Infrastructure.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shared.Observability;

namespace NotificationCore.Api;

/// <summary>
/// Define operações para registrar dependências da API.
/// </summary>
public static class ApiDependencyInjection
{
    /// <summary>
    /// Operação para adicionar os serviços da API.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>Coleção de serviços atualizada.</returns>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddControllers();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddSingleton<UnhandledExceptionMetrics>();
        services.AddSingleton<NotificationDispatchTelemetry>();
        services.AddScoped<NotificationDispatchTelemetryContextReader>();
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddRouting(options => options.LowercaseUrls = true);
        AddHealthChecks(services, configuration);
        services.AddHostedService<RabbitMqNotificationConsumerHostedService>();
        services.AddHostedService<NotificationDispatcherHostedService>();
        AddSwagger(services);

        return services;
    }

    private static void AddHealthChecks(IServiceCollection services, IConfiguration configuration)
    {
        var timeout = GetDependencyTimeout(configuration);
        var rabbitMqOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();
        var dispatcherOptions = configuration.GetSection(NotificationDispatcherOptions.SectionName).Get<NotificationDispatcherOptions>()
            ?? new NotificationDispatcherOptions();
        var healthChecks = services.AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: [HealthCheckTags.Live, HealthCheckTags.Ready])
            .AddCheck<DatabaseHealthCheck>(
                "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthCheckTags.Ready, HealthCheckTags.Dependency, HealthCheckTags.Critical],
                timeout: timeout);

        if (rabbitMqOptions.Enabled)
        {
            healthChecks.AddCheck<RabbitMqHealthCheck>(
                "rabbitmq",
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthCheckTags.Ready, HealthCheckTags.Dependency, HealthCheckTags.Critical],
                timeout: timeout);
        }

        if (dispatcherOptions.Enabled)
        {
            healthChecks.AddCheck<SmtpHealthCheck>(
                "smtp",
                failureStatus: HealthStatus.Degraded,
                tags: [HealthCheckTags.Dependency, HealthCheckTags.Optional],
                timeout: timeout);
        }
    }

    private static TimeSpan GetDependencyTimeout(IConfiguration configuration)
    {
        var seconds = configuration.GetValue("HealthChecks:DependencyTimeoutSeconds", 5);

        return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 30));
    }


    /// <summary>
    /// Operação para adicionar a configuração do Swagger.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            var documentationFileName = $"{typeof(ApiDependencyInjection).Assembly.GetName().Name}.xml";
            var documentationFilePath = Path.Combine(AppContext.BaseDirectory, documentationFileName);

            options.IncludeXmlComments(documentationFilePath, includeControllerXmlComments: true);
        });
    }

}
