using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationCore.Api.Contracts.Responses;
using NotificationCore.Api.Exceptions;
using NotificationCore.Domain.Common.Exceptions;
using Shared.Observability;

namespace NotificationCore.IntegrationTests.Exceptions;

public sealed class ApiExceptionHandlerTests
{
    /// <summary>
    /// Campo que armazena exception handler.
    /// </summary>
    private readonly ApiExceptionHandler _exceptionHandler = new(NullLogger<ApiExceptionHandler>.Instance);

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsDomainException_ShouldReturnBadRequest()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new DomainException("Erro de domínio."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal(["Erro de domínio."], response.Errors);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsUnknown_ShouldReturnInternalServerError()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("Erro interno."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal(["Ocorreu um erro interno inesperado."], response.Errors);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsUnknown_ShouldRecordUnhandledExceptionMetricOnceAndMarkActivityError()
    {
        using var metricCollector = MetricCollector.Listen(UnhandledExceptionMetrics.MeterName);
        using var activityCollector = ActivityCollector.Listen();
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var exceptionHandler = new ApiExceptionHandler(logger, new UnhandledExceptionMetrics());
        var httpContext = CreateHttpContext();
        httpContext.Items[CorrelationIdConstants.HttpContextItemKey] = "corr-notification-secret";
        var exception = new TimeoutException("Timeout com person@example.com e trace secreto.");

        using var activity = activityCollector.StartActivity("notificationcore-handler-test");
        var firstHandled = await exceptionHandler.TryHandleAsync(httpContext, exception, CancellationToken.None);
        ResetResponse(httpContext);
        var secondHandled = await exceptionHandler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        var measurement = Assert.Single(metricCollector.GetMeasurements());
        var instrument = Assert.Single(metricCollector.GetInstruments());

        Assert.True(firstHandled);
        Assert.True(secondHandled);
        Assert.Equal("app.exceptions.unhandled", instrument.Name);
        Assert.Equal("Counter`1", instrument.Kind);
        Assert.Equal("{exception}", instrument.Unit);
        Assert.False(string.IsNullOrWhiteSpace(instrument.Description));
        Assert.Equal(1d, measurement.Value);
        Assert.Equal(["error.type"], measurement.Tags.Keys);
        Assert.Equal("timeout", measurement.Tags["error.type"]);
        Assert.Equal(ActivityStatusCode.Error, activity?.Status);
        Assert.Empty(activity?.Events ?? []);
        Assert.DoesNotContain("TimeoutException", RenderTags(measurement));
        Assert.DoesNotContain("person@example.com", RenderTags(measurement));
        Assert.DoesNotContain("corr-notification-secret", RenderTags(measurement));
        Assert.DoesNotContain("trace secreto", RenderTags(measurement));
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsExpected_ShouldNotRecordUnhandledExceptionMetric()
    {
        using var metricCollector = MetricCollector.Listen(UnhandledExceptionMetrics.MeterName);
        var exceptionHandler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance, new UnhandledExceptionMetrics());

        await exceptionHandler.TryHandleAsync(CreateHttpContext(), new DomainException("Erro de domínio."), CancellationToken.None);

        Assert.Empty(metricCollector.GetMeasurements());
    }

    [Fact]
    public void Record_WhenTraceSamplingWouldDropSpan_ShouldStillRecordUnhandledExceptionMetric()
    {
        using var metricCollector = MetricCollector.Listen(UnhandledExceptionMetrics.MeterName);
        var metrics = new UnhandledExceptionMetrics();
        var httpContext = CreateHttpContext();

        metrics.Record(httpContext, "connectivity");

        var measurement = Assert.Single(metricCollector.GetMeasurements());

        Assert.Equal(1d, measurement.Value);
        Assert.Equal("connectivity", measurement.Tags["error.type"]);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsUnknown_ShouldLogExceptionAndStoreSafeErrorCategory()
    {
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var exceptionHandler = new ApiExceptionHandler(logger);
        var httpContext = CreateHttpContext();
        httpContext.Items[CorrelationIdConstants.HttpContextItemKey] = "corr-notification";

        var wasHandled = await exceptionHandler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("Erro interno com person@example.com."),
            CancellationToken.None);

        var responseText = await ReadResponseTextAsync(httpContext);
        var entry = Assert.Single(logger.Entries);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("unexpected", httpContext.Items[RequestLoggingConstants.ErrorCategoryItemKey]);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.DoesNotContain("InvalidOperationException", responseText);
        Assert.DoesNotContain("person@example.com", responseText);
        Assert.DoesNotContain(" at ", responseText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsKnown_ShouldStoreSafeErrorCategoryWithoutTechnicalLog()
    {
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var exceptionHandler = new ApiExceptionHandler(logger);
        var httpContext = CreateHttpContext();

        var wasHandled = await exceptionHandler.TryHandleAsync(
            httpContext,
            new DomainException("Erro de domínio."),
            CancellationToken.None);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal("validation", httpContext.Items[RequestLoggingConstants.ErrorCategoryItemKey]);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task TryHandleAsync_WhenDomainExceptionHasSensitiveData_ShouldReturnSanitizedError()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new DomainException("Falha no confirmationCode=123456."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);
        var error = Assert.Single(response.Errors);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.DoesNotContain("123456", error);
        Assert.Contains("confirmationCode=[REDACTED]", error);
    }


    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static async Task<ResponseErrorJson> ReadResponseAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;

        return (await JsonSerializer.DeserializeAsync<ResponseErrorJson>(
            httpContext.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken: CancellationToken.None))!;
    }

    private static void ResetResponse(HttpContext httpContext)
    {
        httpContext.Response.Body = new MemoryStream();
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
    }

    private static string RenderTags(CapturedMeasurement measurement)
    {
        return string.Join(" ", measurement.Tags.Select(tag => $"{tag.Key}={tag.Value}"));
    }

    private static async Task<string> ReadResponseTextAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;

        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<CapturedLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new CapturedLogEntry(logLevel, exception));
        }
    }

    private sealed class CapturedLogEntry
    {
        public CapturedLogEntry(LogLevel level, Exception? exception)
        {
            Level = level;
            Exception = exception;
        }

        public LogLevel Level { get; }

        public Exception? Exception { get; }
    }

    private sealed record CapturedInstrument(string MeterName, string Name, string? Unit, string? Description, string Kind);

    private sealed record CapturedMeasurement(
        string MeterName,
        string Name,
        double Value,
        IReadOnlyDictionary<string, object?> Tags);

    private sealed class MetricCollector : IDisposable
    {
        private const string MutexName = "AuthCoreMicroservicesUnhandledExceptionMetricsTests";

        private readonly HashSet<string> _meterNames;
        private readonly object _gate = new();
        private readonly MeterListener _listener = new();
        private readonly Mutex _mutex = new(false, MutexName);

        private MetricCollector(params string[] meterNames)
        {
            _mutex.WaitOne();
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
                        instrument.Description,
                        instrument.GetType().Name));
                }

                listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
                CaptureMeasurement(instrument, measurement, tags));
        }

        private List<CapturedInstrument> Instruments { get; } = [];

        private List<CapturedMeasurement> Measurements { get; } = [];

        public static MetricCollector Listen(params string[] meterNames)
        {
            var collector = new MetricCollector(meterNames);
            collector._listener.Start();

            return collector;
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
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }

        private void CaptureMeasurement(
            Instrument instrument,
            long measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
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

    private sealed class ActivityCollector : IDisposable
    {
        private readonly ActivitySource _activitySource = new("NotificationCore.ExceptionHandler.Tests");
        private readonly ActivityListener _listener;

        private ActivityCollector()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == _activitySource.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public static ActivityCollector Listen()
        {
            return new ActivityCollector();
        }

        public Activity? StartActivity(string name)
        {
            return _activitySource.StartActivity(name);
        }

        public void Dispose()
        {
            _listener.Dispose();
            _activitySource.Dispose();
        }
    }

}
