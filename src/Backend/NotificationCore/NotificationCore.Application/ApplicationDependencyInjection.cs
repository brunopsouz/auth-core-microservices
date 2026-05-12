using Microsoft.Extensions.DependencyInjection;
using NotificationCore.Application.Notifications.UseCases.DispatchPendingNotification;
using NotificationCore.Application.Notifications.UseCases.GetNotification;
using NotificationCore.Application.Notifications.UseCases.RegisterNotificationRequest;
using NotificationCore.Application.Notifications.UseCases.SearchNotifications;
using NotificationCore.Application.Notifications.UseCases.SendTestEmailNotification;

namespace NotificationCore.Application;

/// <summary>
/// Define operações para registrar dependências da aplicação.
/// </summary>
public static class ApplicationDependencyInjection
{
    /// <summary>
    /// Operação para adicionar os serviços da aplicação.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <returns>Coleção de serviços atualizada.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IRegisterNotificationRequestUseCase, RegisterNotificationRequestUseCase>();
        services.AddScoped<IDispatchPendingNotificationUseCase, DispatchPendingNotificationUseCase>();
        services.AddScoped<IGetNotificationUseCase, GetNotificationUseCase>();
        services.AddScoped<ISearchNotificationsUseCase, SearchNotificationsUseCase>();
        services.AddScoped<ISendTestEmailNotificationUseCase, SendTestEmailNotificationUseCase>();

        return services;
    }
}
