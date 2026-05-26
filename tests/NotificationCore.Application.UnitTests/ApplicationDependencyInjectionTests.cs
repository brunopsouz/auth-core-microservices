using Microsoft.Extensions.DependencyInjection;
using NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;
using NotificationCore.Application.UseCases.Notifications.GetNotification;
using NotificationCore.Application.UseCases.Notifications.RegisterNotificationRequest;
using NotificationCore.Application.UseCases.Notifications.SearchNotifications;
using NotificationCore.Application.UseCases.Notifications.SendTestEmailNotification;

namespace NotificationCore.Application.UnitTests;

public sealed class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddApplication_WhenCalled_ShouldRegisterUseCasesAsScoped()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        AssertScopedRegistration<IRegisterNotificationRequestUseCase, RegisterNotificationRequestUseCase>(services);
        AssertScopedRegistration<IDispatchPendingNotificationUseCase, DispatchPendingNotificationUseCase>(services);
        AssertScopedRegistration<IGetNotificationUseCase, GetNotificationUseCase>(services);
        AssertScopedRegistration<ISearchNotificationsUseCase, SearchNotificationsUseCase>(services);
        AssertScopedRegistration<ISendTestEmailNotificationUseCase, SendTestEmailNotificationUseCase>(services);
    }

    private static void AssertScopedRegistration<TService, TImplementation>(IServiceCollection services)
    {
        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(TService));

        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
