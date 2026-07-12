using System.Diagnostics.Metrics;
using Npgsql;

namespace NotificationCore.Infrastructure.Observability;

/// <summary>
/// Representa métricas do acesso PostgreSQL.
/// </summary>
internal sealed class DatabaseMetrics
{
    internal const string MeterName = "notificationcore.database";

    private const string ResultTagName = "result";
    private const string ErrorTypeTagName = "error.type";
    private const string SuccessResult = "success";
    private const string FailureResult = "failure";
    private const string CancelledErrorType = "cancelled";
    private const string ConnectivityErrorType = "connectivity";
    private const string ProtocolErrorType = "protocol";
    private const string TimeoutErrorType = "timeout";
    private const string UnknownErrorType = "unknown";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Histogram<double> AcquisitionDuration = Meter.CreateHistogram<double>(
        "notificationcore.db.connection.acquire.duration",
        unit: "s",
        description: "Duration of PostgreSQL connection acquisition.");
    private static readonly Histogram<double> ConnectionDuration = Meter.CreateHistogram<double>(
        "notificationcore.db.connection.lease.duration",
        unit: "s",
        description: "Duration of PostgreSQL connection lease.");
    private static readonly Histogram<double> TransactionDuration = Meter.CreateHistogram<double>(
        "notificationcore.db.transaction.duration",
        unit: "s",
        description: "Duration of PostgreSQL transaction scope.");
    private static readonly Counter<long> AcquisitionFailures = Meter.CreateCounter<long>(
        "notificationcore.db.connection.acquire.failures",
        unit: "{connection}",
        description: "Number of PostgreSQL connection acquisition failures.");

    public void RecordAcquisition(TimeSpan elapsed, bool succeeded)
    {
        AcquisitionDuration.Record(
            ToNonNegativeSeconds(elapsed),
            new KeyValuePair<string, object?>(ResultTagName, succeeded ? SuccessResult : FailureResult));
    }

    public void RecordConnectionLease(TimeSpan elapsed)
    {
        ConnectionDuration.Record(
            ToNonNegativeSeconds(elapsed),
            new KeyValuePair<string, object?>(ResultTagName, SuccessResult));
    }

    public void RecordTransaction(TimeSpan elapsed)
    {
        TransactionDuration.Record(
            ToNonNegativeSeconds(elapsed),
            new KeyValuePair<string, object?>(ResultTagName, SuccessResult));
    }

    public void RecordAcquisitionFailure(Exception exception)
    {
        AcquisitionFailures.Add(
            1,
            new KeyValuePair<string, object?>(ErrorTypeTagName, NormalizeErrorType(exception)));
    }

    private static double ToNonNegativeSeconds(TimeSpan elapsed)
    {
        return Math.Max(0, elapsed.TotalSeconds);
    }

    private static string NormalizeErrorType(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => CancelledErrorType,
            TimeoutException => TimeoutErrorType,
            NpgsqlException => ConnectivityErrorType,
            InvalidOperationException => ProtocolErrorType,
            _ => UnknownErrorType
        };
    }
}
