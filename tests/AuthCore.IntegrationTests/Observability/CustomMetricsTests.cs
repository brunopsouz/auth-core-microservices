using System.Diagnostics.Metrics;
using AuthCore.Api.Observability;
using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Infrastructure.Services.Messaging;
using Shared.Messaging.Contracts.Notifications;
using DatabaseMetrics = AuthCore.Infrastructure.Observability.DatabaseMetrics;

namespace AuthCore.IntegrationTests.Observability;

public sealed class CustomMetricsTests
{
    [Fact]
    public void AuthCoreMetrics_WhenRecorded_ShouldPublishNormalizedMeasurements()
    {
        using var collector = MetricCollector.Listen(
            "AuthCore.Database",
            "AuthCore.Outbox",
            "AuthCore.ExternalAuthentication",
            DatabaseMetrics.MeterName,
            OutboxMetrics.MeterName,
            AuthBusinessMetrics.MeterName);
        var databaseMetrics = new DatabaseMetrics();
        var outboxMetrics = new OutboxMetrics();
        var authBusinessMetrics = new AuthBusinessMetrics();

        databaseMetrics.RecordAcquisition(TimeSpan.FromMilliseconds(250), succeeded: true);
        databaseMetrics.RecordAcquisition(TimeSpan.FromMilliseconds(125), succeeded: false);
        databaseMetrics.RecordConnectionLease(TimeSpan.FromMilliseconds(500));
        databaseMetrics.RecordTransaction(TimeSpan.FromSeconds(2));
        databaseMetrics.RecordTransaction(TimeSpan.FromMilliseconds(-1));
        databaseMetrics.RecordAcquisitionFailure(new InvalidOperationException("password token user@example.com corr-123 trace-123"));
        outboxMetrics.RecordProcessed(nameof(SendTransactionalNotificationRequested));
        outboxMetrics.RecordFailed(
            "free-text-" + Guid.NewGuid().ToString("D"),
            new InvalidOperationException("raw exception text"));
        outboxMetrics.RecordDuration(TimeSpan.FromMilliseconds(750), "processed");
        outboxMetrics.RecordDuration(TimeSpan.FromMilliseconds(175), "failure");
        authBusinessMetrics.RecordAuthenticationAttempt(
            AuthBusinessMetrics.FlowPasswordSession,
            AuthBusinessMetrics.ResultSuccess,
            AuthBusinessMetrics.ReasonNone);
        authBusinessMetrics.RecordAuthenticationAttempt(
            AuthBusinessMetrics.FlowPasswordToken,
            AuthBusinessMetrics.ResultBlocked,
            AuthBusinessMetrics.ReasonRateLimited);
        authBusinessMetrics.RecordAuthenticationAttempt(
            AuthBusinessMetrics.FlowGoogleSession,
            AuthBusinessMetrics.ResultFailure,
            AuthBusinessMetrics.ReasonValidationError);
        authBusinessMetrics.RecordRefreshTokenOperation(
            AuthBusinessMetrics.OperationRotate,
            AuthBusinessMetrics.ResultFailure,
            "reused_token");
        authBusinessMetrics.RecordSessionOperation(
            AuthBusinessMetrics.OperationRevokeAll,
            AuthBusinessMetrics.ResultSuccess,
            AuthBusinessMetrics.ReasonNone);
        authBusinessMetrics.RecordRegistrationAttempt(
            AuthBusinessMetrics.ResultFailure,
            AuthBusinessMetrics.ReasonDuplicateEmail);
        authBusinessMetrics.RecordEmailVerificationAttempt(
            AuthBusinessMetrics.ResultFailure,
            AuthBusinessMetrics.ReasonInvalidToken);
        var instruments = collector.GetInstruments();
        var measurements = collector.GetMeasurements();

        collector.AssertInstrument("authcore.db.connection.acquire.duration", "s");
        collector.AssertInstrument("authcore.db.connection.lease.duration", "s");
        collector.AssertInstrument("authcore.db.transaction.duration", "s");
        collector.AssertInstrument("authcore.db.connection.acquire.failures", "{connection}");
        collector.AssertInstrument("authcore.outbox.messages.processed", "{message}");
        collector.AssertInstrument("authcore.outbox.messages.failed", "{message}");
        collector.AssertInstrument("authcore.outbox.processing.duration", "s");
        collector.AssertInstrument("authcore.authentication.attempts", "{attempt}");
        collector.AssertInstrument("authcore.refresh_tokens.operations", "{operation}");
        collector.AssertInstrument("authcore.sessions.operations", "{operation}");
        collector.AssertInstrument("authcore.registration.attempts", "{attempt}");
        collector.AssertInstrument("authcore.email_verification.attempts", "{attempt}");

        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.db.connection.acquire.duration"
            && measurement.Value == 0.25d
            && Convert.ToString(measurement.Tags["result"]) == "success");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.db.connection.acquire.duration"
            && measurement.Value == 0.125d
            && Convert.ToString(measurement.Tags["result"]) == "failure");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.db.connection.acquire.failures"
            && measurement.Value == 1d
            && Convert.ToString(measurement.Tags["error.type"]) == "protocol");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.outbox.messages.processed"
            && Convert.ToString(measurement.Tags["event_type"]) == "send_transactional_notification_requested"
            && Convert.ToString(measurement.Tags["result"]) == "processed");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.outbox.messages.failed"
            && Convert.ToString(measurement.Tags["event_type"]) == "unknown"
            && Convert.ToString(measurement.Tags["reason"]) == "protocol");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.authentication.attempts"
            && Convert.ToString(measurement.Tags["flow"]) == "google_session"
            && Convert.ToString(measurement.Tags["result"]) == "failure"
            && Convert.ToString(measurement.Tags["reason"]) == "validation_error");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.authentication.attempts"
            && Convert.ToString(measurement.Tags["flow"]) == "password_token"
            && Convert.ToString(measurement.Tags["result"]) == "blocked"
            && Convert.ToString(measurement.Tags["reason"]) == "rate_limited");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.refresh_tokens.operations"
            && Convert.ToString(measurement.Tags["operation"]) == "rotate"
            && Convert.ToString(measurement.Tags["result"]) == "failure"
            && Convert.ToString(measurement.Tags["reason"]) == "reused_token");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.sessions.operations"
            && Convert.ToString(measurement.Tags["operation"]) == "revoke_all"
            && Convert.ToString(measurement.Tags["result"]) == "success"
            && Convert.ToString(measurement.Tags["reason"]) == "none");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.registration.attempts"
            && Convert.ToString(measurement.Tags["result"]) == "failure"
            && Convert.ToString(measurement.Tags["reason"]) == "duplicate_email");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.email_verification.attempts"
            && Convert.ToString(measurement.Tags["result"]) == "failure"
            && Convert.ToString(measurement.Tags["reason"]) == "invalid_token");
        Assert.DoesNotContain(measurements, measurement =>
            measurement.Tags.ContainsKey("provider"));
        Assert.DoesNotContain(measurements, measurement =>
            measurement.Name.StartsWith("authcore.auth.external.", StringComparison.Ordinal));
        Assert.Contains(measurements, measurement =>
            measurement.Name == "authcore.outbox.processing.duration"
            && measurement.Value == 0.175d
            && Convert.ToString(measurement.Tags["result"]) == "failure");

        Assert.All(measurements, measurement => Assert.True(measurement.Value >= 0));
        Assert.DoesNotContain(instruments, instrument => instrument.MeterName is
            "AuthCore.Database"
            or "AuthCore.Outbox"
            or "AuthCore.ExternalAuthentication");
        Assert.DoesNotContain(instruments, instrument => instrument.Name.Contains("_total", StringComparison.Ordinal));
        Assert.DoesNotContain(instruments, instrument => instrument.Name.EndsWith(".ms", StringComparison.Ordinal));
        Assert.DoesNotContain(instruments, instrument => instrument.Name == "auth_google_callback_duration_ms");
        Assert.DoesNotContain(instruments, instrument => instrument.Name == "authcore.auth.external.callback.duration");
        Assert.DoesNotContain(instruments, instrument => instrument.Name == "authcore.database.connection.acquisition.duration.ms");
        Assert.DoesNotContain(measurements, ContainsForbiddenMetricValue);
    }

    private static bool ContainsForbiddenMetricValue(CapturedMeasurement measurement)
    {
        var renderedTags = string.Join(" ", measurement.Tags.Select(tag => $"{tag.Key}={tag.Value}"));

        return renderedTags.Contains("user@example.com", StringComparison.OrdinalIgnoreCase)
            || renderedTags.Contains("corr-123", StringComparison.OrdinalIgnoreCase)
            || renderedTags.Contains("trace-123", StringComparison.OrdinalIgnoreCase)
            || renderedTags.Contains("InvalidOperationException", StringComparison.OrdinalIgnoreCase)
            || renderedTags.Contains("free-text", StringComparison.OrdinalIgnoreCase)
            || renderedTags.Contains(nameof(EmailVerificationRequested), StringComparison.Ordinal);
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
