using System.Diagnostics;
using System.Reflection;
using OpenTelemetry;

namespace AuthCore.Infrastructure.Observability;

/// <summary>
/// Representa processor para remover payloads sensíveis de spans Npgsql.
/// </summary>
public sealed class NpgsqlTelemetryActivityProcessor : BaseProcessor<Activity>
{
    private const string ActivitySourceName = "Npgsql";

    private static readonly FieldInfo? EventsField = typeof(Activity)
        .GetField("_events", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly string[] SensitiveAttributeNames =
    [
        "db.statement",
        "db.query.text",
        "db.npgsql.command_text",
        "exception.message",
        "exception.stacktrace",
        "exception.type"
    ];

    public override void OnEnd(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (!string.Equals(activity.Source.Name, ActivitySourceName, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var attributeName in SensitiveAttributeNames)
        {
            activity.SetTag(attributeName, null);
        }

        EventsField?.SetValue(activity, null);
    }
}
