using Microsoft.Extensions.DependencyInjection;
using NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;
using NotificationCore.Application.UseCases.Notifications.GetNotification;
using NotificationCore.Application.UseCases.Notifications.ListActiveNotificationTemplates;
using NotificationCore.Application.UseCases.Notifications.RegisterNotificationRequest;
using NotificationCore.Application.UseCases.Notifications.SearchNotifications;
using NotificationCore.Application.UseCases.Notifications.SendTestEmailNotification;

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
        services.AddScoped<ITimedOutNotificationRecovery, TimedOutNotificationRecovery>();
        services.AddScoped<IPendingNotificationDispatcher, PendingNotificationDispatcher>();
        services.AddScoped<IDispatchPendingNotificationUseCase, DispatchPendingNotificationUseCase>();
        services.AddScoped<IGetNotificationUseCase, GetNotificationUseCase>();
        services.AddScoped<ISearchNotificationsUseCase, SearchNotificationsUseCase>();
        services.AddScoped<ISendTestEmailNotificationUseCase, SendTestEmailNotificationUseCase>();
        services.AddScoped<IListActiveNotificationTemplatesUseCase, ListActiveNotificationTemplatesUseCase>();

        return services;
    }
}
