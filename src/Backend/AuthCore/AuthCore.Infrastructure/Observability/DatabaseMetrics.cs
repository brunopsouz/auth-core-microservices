using System.Diagnostics.Metrics;

namespace AuthCore.Infrastructure.Observability;

/// <summary>
/// Representa métricas do acesso PostgreSQL.
/// </summary>
internal sealed class DatabaseMetrics
{
    private static readonly Meter Meter = new("AuthCore.Database", "1.0.0");
    private static readonly Histogram<double> AcquisitionDuration = Meter.CreateHistogram<double>(
        "authcore.database.connection.acquisition.duration.ms");
    private static readonly Histogram<double> ConnectionDuration = Meter.CreateHistogram<double>(
        "authcore.database.connection.lease.duration.ms");
    private static readonly Histogram<double> TransactionDuration = Meter.CreateHistogram<double>(
        "authcore.database.transaction.duration.ms");
    private static readonly Counter<long> AcquisitionFailures = Meter.CreateCounter<long>(
        "authcore.database.connection.acquisition.failures");

    public void RecordAcquisition(TimeSpan elapsed) => AcquisitionDuration.Record(elapsed.TotalMilliseconds);
    public void RecordConnectionLease(TimeSpan elapsed) => ConnectionDuration.Record(elapsed.TotalMilliseconds);
    public void RecordTransaction(TimeSpan elapsed) => TransactionDuration.Record(elapsed.TotalMilliseconds);
    public void RecordAcquisitionFailure() => AcquisitionFailures.Add(1);
}
