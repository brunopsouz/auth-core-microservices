using NotificationCore.Api.Exceptions;
using NotificationCore.Api.HealthChecks;
using NotificationCore.Api.Workers;
using Microsoft.Extensions.Configuration;

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
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddRouting(options => options.LowercaseUrls = true);
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("postgresql")
            .AddCheck<RabbitMqHealthCheck>("rabbitmq")
            .AddCheck<NotificationDispatcherHealthCheck>("dispatcher");
        services.AddHostedService<RabbitMqNotificationConsumerHostedService>();
        services.AddHostedService<NotificationDispatcherHostedService>();
        AddSwagger(services);

        return services;
    }

    #region Helpers

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

    #endregion
}
