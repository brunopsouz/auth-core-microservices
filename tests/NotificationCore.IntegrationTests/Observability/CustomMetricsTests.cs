using System.Diagnostics.Metrics;
using NotificationCore.Infrastructure.Observability;

namespace NotificationCore.IntegrationTests.Observability;

public sealed class CustomMetricsTests
{
    [Fact]
    public void NotificationCoreMetrics_WhenRecorded_ShouldPublishNormalizedMeasurements()
    {
        using var collector = MetricCollector.Listen(
            "NotificationCore.Database",
            "NotificationCore.Notifications",
            DatabaseMetrics.MeterName,
            NotificationMetrics.MeterName);
        var databaseMetrics = new DatabaseMetrics();
        var notificationMetrics = new NotificationMetrics();

        databaseMetrics.RecordAcquisition(TimeSpan.FromMilliseconds(300), succeeded: true);
        databaseMetrics.RecordAcquisition(TimeSpan.FromMilliseconds(125), succeeded: false);
        databaseMetrics.RecordConnectionLease(TimeSpan.FromMilliseconds(450));
        databaseMetrics.RecordTransaction(TimeSpan.FromMilliseconds(600));
        databaseMetrics.RecordAcquisitionFailure(new TimeoutException("trace-123 correlation-123 user@example.com"));
        notificationMetrics.RecordPending(3);
        notificationMetrics.RecordPending(-1);
        notificationMetrics.RecordSent(2);
        notificationMetrics.RecordFailed(1);
        notificationMetrics.RecordDispatchDuration(TimeSpan.FromMilliseconds(900));
        notificationMetrics.RecordDispatchDuration(TimeSpan.FromMilliseconds(-1));
        notificationMetrics.RecordSendDuration(TimeSpan.FromMilliseconds(150), "smtp.example.com");
        var instruments = collector.GetInstruments();
        var measurements = collector.GetMeasurements();

        collector.AssertInstrument("notificationcore.db.connection.acquire.duration", "s");
        collector.AssertInstrument("notificationcore.db.connection.lease.duration", "s");
        collector.AssertInstrument("notificationcore.db.transaction.duration", "s");
        collector.AssertInstrument("notificationcore.db.connection.acquire.failures", "{connection}");
        collector.AssertInstrument("notificationcore.notifications.pending", "{notification}");
        collector.AssertInstrument("notificationcore.notifications.sent", "{notification}");
        collector.AssertInstrument("notificationcore.notifications.failed", "{notification}");
        collector.AssertInstrument("notificationcore.notifications.dispatch.duration", "s");
        collector.AssertInstrument("notificationcore.notifications.send.duration", "s");

        Assert.Contains(measurements, measurement =>
            measurement.Name == "notificationcore.db.connection.acquire.duration"
            && measurement.Value == 0.3d
            && Convert.ToString(measurement.Tags["result"]) == "success");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "notificationcore.db.connection.acquire.duration"
            && measurement.Value == 0.125d
            && Convert.ToString(measurement.Tags["result"]) == "failure");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "notificationcore.db.connection.acquire.failures"
            && measurement.Value == 1d
            && Convert.ToString(measurement.Tags["error.type"]) == "timeout");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "notificationcore.notifications.pending"
            && measurement.Value == 3d);
        Assert.DoesNotContain(measurements, measurement =>
            measurement.Name == "notificationcore.notifications.pending"
            && measurement.Value < 0d);
        Assert.Contains(measurements, measurement =>
            measurement.Name == "notificationcore.notifications.dispatch.duration"
            && measurement.Value == 0.9d);
        Assert.Contains(measurements, measurement =>
            measurement.Name == "notificationcore.notifications.send.duration"
            && measurement.Value == 0.15d
            && Convert.ToString(measurement.Tags["provider"]) == "unknown");

        Assert.All(measurements, measurement => Assert.True(measurement.Value >= 0));
        Assert.DoesNotContain(instruments, instrument => instrument.MeterName is
            "NotificationCore.Database"
            or "NotificationCore.Notifications");
        Assert.DoesNotContain(instruments, instrument => instrument.Name.Contains("_total", StringComparison.Ordinal));
        Assert.DoesNotContain(instruments, instrument => instrument.Name.EndsWith(".ms", StringComparison.Ordinal));
        Assert.DoesNotContain(instruments, instrument => instrument.Name == "notificationcore.database.connection.acquisition.duration.ms");
        Assert.DoesNotContain(measurements, ContainsForbiddenMetricValue);
    }

    private static bool ContainsForbiddenMetricValue(CapturedMeasurement measurement)
    {
        var renderedTags = string.Join(" ", measurement.Tags.Select(tag => $"{tag.Key}={tag.Value}"));

        return renderedTags.Contains("trace-123", StringComparison.OrdinalIgnoreCase)
            || renderedTags.Contains("correlation-123", StringComparison.OrdinalIgnoreCase)
            || renderedTags.Contains("user@example.com", StringComparison.OrdinalIgnoreCase)
            || renderedTags.Contains("TimeoutException", StringComparison.OrdinalIgnoreCase)
            || renderedTags.Contains("smtp.example.com", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CapturedInstrument(string MeterName, string Name, string? Unit, string? Description);

    private sealed record CapturedMeasurement(
        string MeterName,
        string Name,
        double Value,
        IReadOnlyDictionary<string, object?> Tags);

    private sealed class MetricCollector : IDisposable
    {
        private readonly HashSet<string> _meterNames;
        private readonly object _gate = new();
        private readonly MeterListener _listener = new();

        private MetricCollector(params string[] meterNames)
        {
            _meterNames = meterNames.ToHashSet(StringComparer.Ordinal);
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (!_meterNames.Contains(instrument.Meter.Name))
                    return;

                lock (_gate)
                {
                    Instruments.Add(new CapturedInstrument(
                        instrument.Meter.Name,
                        instrument.Name,
                        instrument.Unit,
                        instrument.Description));
                }
                listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<double>(CaptureMeasurement);
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
                CaptureMeasurement(instrument, measurement, tags, null));
        }

        public List<CapturedInstrument> Instruments { get; } = [];

        public List<CapturedMeasurement> Measurements { get; } = [];

        public static MetricCollector Listen(params string[] meterNames)
        {
            var collector = new MetricCollector(meterNames);
            collector._listener.Start();

            return collector;
        }

        public void AssertInstrument(string name, string unit)
        {
            var instrument = Assert.Single(GetInstruments().Where(candidate => candidate.Name == name));

            Assert.Equal(unit, instrument.Unit);
            Assert.False(string.IsNullOrWhiteSpace(instrument.Description));
        }

        public CapturedInstrument[] GetInstruments()
        {
            lock (_gate)
            {
                return [.. Instruments];
            }
        }

        public CapturedMeasurement[] GetMeasurements()
        {
            lock (_gate)
            {
                return [.. Measurements];
            }
        }

        public void Dispose()
        {
            _listener.Dispose();
        }

        private void CaptureMeasurement(
            Instrument instrument,
            double measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? _)
        {
            if (!_meterNames.Contains(instrument.Meter.Name))
                return;

            lock (_gate)
            {
                Measurements.Add(new CapturedMeasurement(
                    instrument.Meter.Name,
                    instrument.Name,
                    measurement,
                    tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
            }
        }
    }
}
