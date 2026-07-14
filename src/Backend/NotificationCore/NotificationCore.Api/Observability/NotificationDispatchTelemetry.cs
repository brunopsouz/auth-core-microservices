using System.Diagnostics;
using NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;
using NotificationCore.Domain.Notifications.Enums;

namespace NotificationCore.Api.Observability;

/// <summary>
/// Representa telemetria do despacho de notificações.
/// </summary>
internal sealed class NotificationDispatchTelemetry
{
    internal const string ActivitySourceName = "notificationcore.notifications";

    private const string EmailVerificationTemplateKey = "auth.email-confirmation";
    private const string TestEmailTemplateKey = "notificationcore.test-email";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Operação para rastrear tecnicamente um ciclo de despacho.
    /// </summary>
    /// <param name="contexts">Contextos técnicos seguros do despacho.</param>
    /// <param name="operation">Operação de despacho.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado funcional do despacho.</returns>
    public async Task<DispatchPendingNotificationResult> TrackAsync(
        IReadOnlyCollection<NotificationDispatchTelemetryContext> contexts,
        Func<Task<DispatchPendingNotificationResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(operation);

        using var activity = StartDispatchActivity(contexts);

        try
        {
            var result = await operation();
            ApplyResult(activity, MapResult(result));

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ApplyResult(activity, NotificationDispatchTelemetryResult.Cancelled);
            throw;
        }
        catch
        {
            ApplyResult(activity, NotificationDispatchTelemetryResult.Failure);
            throw;
        }
    }

    private static Activity? StartDispatchActivity(IReadOnlyCollection<NotificationDispatchTelemetryContext> contexts)
    {
        var links = CreateLinks(contexts);
        var activity = links.Count == 0
            ? ActivitySource.StartActivity("notification dispatch", ActivityKind.Internal)
            : ActivitySource.StartActivity(
                "notification dispatch",
                ActivityKind.Internal,
                parentContext: default,
                tags: null,
                links: links);

        ApplyContextTags(activity, contexts);

        return activity;
    }

    private static IReadOnlyCollection<ActivityLink> CreateLinks(
        IReadOnlyCollection<NotificationDispatchTelemetryContext> contexts)
    {
        if (Activity.Current is not null || contexts.Count == 0)
            return [];

        var links = new List<ActivityLink>();

        foreach (var context in contexts)
        {
            if (!string.IsNullOrWhiteSpace(context.TraceParent)
                && ActivityContext.TryParse(context.TraceParent, context.TraceState, out var linkedContext))
            {
                links.Add(new ActivityLink(linkedContext));
            }
        }

        return links;
    }

    private static void ApplyContextTags(
        Activity? activity,
        IReadOnlyCollection<NotificationDispatchTelemetryContext> contexts)
    {
        if (activity is null)
            return;

        if (contexts.Count == 1)
        {
            var context = contexts.Single();
            activity.SetTag("notification.type", MapNotificationType(context.TemplateKey));
            activity.SetTag("notification.channel", MapChannel(context.Channel));
            activity.SetTag("notification.retry", context.IsRetry);
            return;
        }

        activity.SetTag("notification.channel", "email");
        activity.SetTag("notification.retry", contexts.Any(context => context.IsRetry));
    }

    private static void ApplyResult(Activity? activity, NotificationDispatchTelemetryResult result)
    {
        if (activity is null)
            return;

        activity.SetTag("notification.result", MapResult(result));

        if (result is not NotificationDispatchTelemetryResult.Success)
            activity.SetStatus(ActivityStatusCode.Error);
    }

    internal static string MapNotificationType(string templateKey)
    {
        if (string.Equals(templateKey, EmailVerificationTemplateKey, StringComparison.Ordinal))
            return "email_verification";

        if (string.Equals(templateKey, TestEmailTemplateKey, StringComparison.Ordinal))
            return "test_email";

        return "other_transactional";
    }

    private static NotificationDispatchTelemetryResult MapResult(DispatchPendingNotificationResult result)
    {
        if (result.DeadLettered > 0)
            return NotificationDispatchTelemetryResult.Failure;

        if (result.RetryScheduled > 0)
            return NotificationDispatchTelemetryResult.RetryScheduled;

        return NotificationDispatchTelemetryResult.Success;
    }

    private static string MapChannel(NotificationChannel channel)
    {
        return channel == NotificationChannel.Email
            ? "email"
            : "unknown";
    }

    private static string MapResult(NotificationDispatchTelemetryResult result)
    {
        return result switch
        {
            NotificationDispatchTelemetryResult.Success => "success",
            NotificationDispatchTelemetryResult.Cancelled => "cancelled",
            NotificationDispatchTelemetryResult.RetryScheduled => "retry_scheduled",
            _ => "failure"
        };
    }
}
