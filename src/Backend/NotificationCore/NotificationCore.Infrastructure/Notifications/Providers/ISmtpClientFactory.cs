namespace NotificationCore.Infrastructure.Notifications.Providers;

internal interface ISmtpClientFactory
{
    ISmtpClientAdapter Create();
}
