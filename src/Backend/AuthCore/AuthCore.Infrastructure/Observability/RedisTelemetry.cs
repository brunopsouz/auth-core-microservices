using System.Diagnostics;
using System.Diagnostics.Metrics;
using StackExchange.Redis;

namespace AuthCore.Infrastructure.Observability;

/// <summary>
/// Representa telemetria segura para operações Redis do AuthCore.
/// </summary>
internal static class RedisTelemetry
{
    internal const string MeterName = "authcore.redis";
    internal const string ActivitySourceName = "authcore.redis";

    internal const string OperationGet = "get";
    internal const string OperationSet = "set";
    internal const string OperationDelete = "delete";
    internal const string OperationEval = "eval";
    internal const string OperationExpire = "expire";

    private const string ResultSuccess = "success";
    private const string ResultFailure = "failure";
    private const string ResultCancelled = "cancelled";

    private const string ErrorTimeout = "timeout";
    private const string ErrorConnectivity = "connectivity";
    private const string ErrorAuthentication = "authentication";
    private const string ErrorProtocol = "protocol";
    private const string ErrorCancelled = "cancelled";
    private const string ErrorUnknown = "unknown";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "authcore.redis.operation.duration",
        "s",
        "Duration of Redis operations performed by AuthCore.");

    private static readonly Counter<long> OperationFailures = Meter.CreateCounter<long>(
        "authcore.redis.operation.failures",
        "{operation}",
        "Number of failed Redis operations performed by AuthCore.");

    internal static async Task<T> TrackAsync<T>(
        string operation,
        Func<Task<T>> redisOperation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(redisOperation);

        var startedAt = Stopwatch.GetTimestamp();
        using var activity = StartActivity(operation);

        try
        {
            var result = await redisOperation();
            RecordDuration(operation, ResultSuccess, startedAt);

            return result;
        }
        catch (OperationCanceledException)
        {
            RecordError(activity, operation, ErrorCancelled, ResultCancelled, startedAt);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = MapErrorType(exception);
            RecordError(activity, operation, errorType, ResultFailure, startedAt);
            throw;
        }
    }

    internal static async Task TrackAsync(
        string operation,
        Func<Task> redisOperation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(redisOperation);

        await TrackAsync(operation, async () =>
        {
            await redisOperation();
            return true;
        });
    }

    internal static string MapErrorType(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException => ErrorCancelled,
            RedisTimeoutException => ErrorTimeout,
            RedisConnectionException connectionException
                when IsAuthenticationFailure(connectionException) => ErrorAuthentication,
            RedisConnectionException => ErrorConnectivity,
            RedisServerException => ErrorProtocol,
            _ => ErrorUnknown
        };
    }

    private static Activity? StartActivity(string operation)
    {
        var activity = ActivitySource.StartActivity($"redis {operation}", ActivityKind.Client);
        activity?.SetTag("db.system.name", "redis");
        activity?.SetTag("db.operation.name", operation);

        return activity;
    }

    private static bool IsAuthenticationFailure(RedisConnectionException exception)
    {
        return exception.FailureType
            .ToString()
            .Contains("Authentication", StringComparison.OrdinalIgnoreCase);
    }

    private static void RecordError(
        Activity? activity,
        string operation,
        string errorType,
        string result,
        long startedAt)
    {
        activity?.SetStatus(ActivityStatusCode.Error);
        activity?.SetTag("error.type", errorType);
        OperationFailures.Add(1, new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error.type", errorType));
        RecordDuration(operation, result, startedAt);
    }

    private static void RecordDuration(string operation, string result, long startedAt)
    {
        var elapsedSeconds = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;

        OperationDuration.Record(elapsedSeconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", result));
    }
}
