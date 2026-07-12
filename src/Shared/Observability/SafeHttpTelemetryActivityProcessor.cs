using System.Diagnostics;
using OpenTelemetry;

namespace Shared.Observability;

/// <summary>
/// Representa processor para remover atributos HTTP sensíveis antes da exportação.
/// </summary>
internal sealed class SafeHttpTelemetryActivityProcessor : BaseProcessor<Activity>
{
    private static readonly string[] SensitiveAttributeNames =
    [
        "http.url",
        "http.target",
        "url.full",
        "url.path",
        "url.query",
        "exception.message",
        "exception.stacktrace",
        "http.request.header.authorization",
        "http.request.header.cookie",
        "http.response.header.set-cookie"
    ];

    public override void OnEnd(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        foreach (var attributeName in SensitiveAttributeNames)
        {
            activity.SetTag(attributeName, null);
        }
    }
}
