using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;

namespace Shared.Observability;

/// <summary>
/// Representa métricas transversais de exceções inesperadas.
/// </summary>
public sealed class UnhandledExceptionMetrics
{
    /// <summary>
    /// Nome do medidor transversal de observabilidade.
    /// </summary>
    public const string MeterName = "app.observability";

    private const string MetricRecordedItemKey = "Observability.UnhandledExceptionMetricRecorded";
    private const string ErrorTypeTagName = "error.type";
    private const string AuthenticationErrorType = "authentication";
    private const string CancelledErrorType = "cancelled";
    private const string ConnectivityErrorType = "connectivity";
    private const string ProtocolErrorType = "protocol";
    private const string TimeoutErrorType = "timeout";
    private const string UnknownErrorType = "unknown";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> UnhandledExceptions = Meter.CreateCounter<long>(
        "app.exceptions.unhandled",
        unit: "{exception}",
        description: "Number of unexpected exceptions handled by the application's global exception handler.");

    /// <summary>
    /// Operação para registrar uma exceção inesperada.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP atual.</param>
    /// <param name="errorType">Categoria técnica normalizada.</param>
    public void Record(HttpContext httpContext, string errorType)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Items.ContainsKey(MetricRecordedItemKey))
        {
            return;
        }

        httpContext.Items[MetricRecordedItemKey] = true;
        UnhandledExceptions.Add(
            1,
            new KeyValuePair<string, object?>(ErrorTypeTagName, NormalizeErrorType(errorType)));
    }

    private static string NormalizeErrorType(string errorType)
    {
        return errorType switch
        {
            AuthenticationErrorType => AuthenticationErrorType,
            CancelledErrorType => CancelledErrorType,
            ConnectivityErrorType => ConnectivityErrorType,
            ProtocolErrorType => ProtocolErrorType,
            TimeoutErrorType => TimeoutErrorType,
            _ => UnknownErrorType
        };
    }
}
