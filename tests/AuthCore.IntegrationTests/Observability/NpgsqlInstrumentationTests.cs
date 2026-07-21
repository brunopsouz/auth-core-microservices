using System.Diagnostics;
using AuthCore.Infrastructure;
using AuthCore.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shared.Observability;
using Xunit;

namespace AuthCore.IntegrationTests.Observability;

public sealed class NpgsqlInstrumentationTests
{
    private const string ActivitySourceName = "AuthCore.IntegrationTests.Npgsql";
    private const string DataSourceName = "authcore-postgresql";
    private const string MeterName = "Npgsql";
    private const string PasswordSentinel = "password-sentinel-observability";
    private const string UsernameSentinel = "username-sentinel-observability";
    private const string HostSentinel = "host-sentinel-observability";
    private const string SqlSentinel = "sql-sentinel-observability";
    private const string ParameterSentinel = "parameter-sentinel-observability";
    private const string EmailSentinel = "user@example-observability.invalid";

    [PostgreSqlFact("AUTHCORE_TEST_POSTGRES")]
    public async Task AddInfrastructure_WhenDataSourceIsResolved_ShouldUseSafeLogicalName()
    {
        await using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(CreateInfrastructureConfiguration())
            .BuildServiceProvider();

        var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
        var builder = new NpgsqlDataSourceBuilder(CreateInfrastructureConnectionString())
        {
            Name = DataSourceName
        };
        var configuredName = builder.GetType().GetProperty("Name")?.GetValue(builder)?.ToString();

        Assert.NotNull(dataSource);
        Assert.Equal(DataSourceName, configuredName);
        Assert.DoesNotContain(HostSentinel, configuredName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(UsernameSentinel, configuredName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PasswordSentinel, configuredName, StringComparison.OrdinalIgnoreCase);
    }

    [PostgreSqlFact("AUTHCORE_TEST_POSTGRES")]
    public async Task AddObservability_WhenNpgsqlCommandRuns_ShouldExportSafeChildSpanAndNativeMetrics()
    {
        await EnsurePostgreSqlAvailableAsync();

        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1");
        await host.StartAsync();

        var parentContext = await ExecuteSuccessfulCommandAsync(requireParentActivity: true);
        await ExecuteFailingCommandAsync();
        await ForceFlushUntilAsync(host, () => ContainsOperationDurationMetric(telemetry.Metrics));
        await host.StopAsync();

        var clientSpans = telemetry.Activities.Where(activity =>
            IsSuccessfulNpgsqlCommandSpan(activity)
            && activity.ParentSpanId == parentContext.SpanId
            && activity.TraceId == parentContext.TraceId)
            .ToList();
        var npgsqlMetrics = telemetry.Metrics.Where(IsNpgsqlMetric).ToList();
        Assert.NotEmpty(clientSpans);
        var clientSpan = clientSpans.First();
        var errorSpan = Assert.Single(telemetry.Activities.Where(IsFailedNpgsqlCommandSpan));
        var operationMetrics = npgsqlMetrics
            .Where(metric => metric.Name == "db.client.operation.duration")
            .ToList();
        Assert.True(operationMetrics.Count > 0, RenderTelemetry(clientSpans.Append(errorSpan), telemetry.Metrics));
        var operationMetric = operationMetrics.First();
        var renderedTelemetry = RenderTelemetry(clientSpans.Append(errorSpan), npgsqlMetrics);

        Assert.Equal(ActivityKind.Client, clientSpan.Kind);
        Assert.Equal(ActivityKind.Client, errorSpan.Kind);
        Assert.Equal(ActivityStatusCode.Error, errorSpan.Status);
        Assert.Equal(parentContext.TraceId, clientSpan.TraceId);
        Assert.Equal(parentContext.SpanId, clientSpan.ParentSpanId);
        Assert.NotEqual(clientSpan.SpanId, clientSpan.ParentSpanId);
        Assert.Equal("s", operationMetric.Unit);
        Assert.Contains(npgsqlMetrics, metric => metric.Name.StartsWith("db.client.connection.", StringComparison.Ordinal));
        Assert.DoesNotContain("db.statement", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db.query.text", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Username=", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SqlSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ParameterSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EmailSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT @value", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception.message", RenderActivity(errorSpan), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception.stacktrace", RenderActivity(errorSpan), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(telemetry.Metrics, metric => metric.MeterName == DatabaseMetrics.MeterName
            && metric.Name == "db.client.operation.duration");
    }

    [PostgreSqlFact("AUTHCORE_TEST_POSTGRES")]
    public async Task AddObservability_WhenSamplingIsZero_ShouldNotExportNpgsqlSpansButShouldExportMetrics()
    {
        await EnsurePostgreSqlAvailableAsync();

        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "0");
        await host.StartAsync();

        await ExecuteSuccessfulCommandAsync(requireParentActivity: true);
        await ForceFlushUntilAsync(host, () => ContainsOperationDurationMetric(telemetry.Metrics));
        await host.StopAsync();

        Assert.DoesNotContain(telemetry.Activities, activity => activity.Source.Name == MeterName);
        Assert.True(
            ContainsOperationDurationMetric(telemetry.Metrics),
            RenderTelemetry([], telemetry.Metrics));
    }

    [PostgreSqlFact("AUTHCORE_TEST_POSTGRES")]
    public async Task AddObservability_WhenDisabled_ShouldNotCreateProvidersOrBlockDatabaseAccess()
    {
        await EnsurePostgreSqlAvailableAsync();

        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", enabled: false);
        await host.StartAsync();

        await ExecuteSuccessfulCommandAsync(requireParentActivity: false);

        Assert.Null(host.Services.GetService<TracerProvider>());
        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.Empty(telemetry.Activities);
        Assert.Empty(telemetry.Metrics);
    }

    private static async Task<ParentActivityContext> ExecuteSuccessfulCommandAsync(bool requireParentActivity)
    {
        using var activitySource = new ActivitySource(ActivitySourceName);
        using var parent = activitySource.StartActivity("npgsql-parent", ActivityKind.Internal);
        if (requireParentActivity)
        {
            Assert.NotNull(parent);
        }

        await using var dataSource = BuildDataSource(GetRequiredConnectionString());
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT @value::text /* {SqlSentinel} {EmailSentinel} */",
            connection);
        command.Parameters.AddWithValue("value", ParameterSentinel);

        Assert.Equal(ParameterSentinel, await command.ExecuteScalarAsync());

        return parent is null
            ? new ParentActivityContext(default, default)
            : new ParentActivityContext(parent.TraceId, parent.SpanId);
    }

    private static async Task ExecuteFailingCommandAsync()
    {
        using var activitySource = new ActivitySource(ActivitySourceName);
        using var parent = activitySource.StartActivity("npgsql-parent-failure", ActivityKind.Internal);
        Assert.NotNull(parent);

        await using var dataSource = BuildDataSource(GetRequiredConnectionString());
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT 1 / @value", connection);
        command.Parameters.AddWithValue("value", 0);

        _ = await Assert.ThrowsAnyAsync<PostgresException>(() => command.ExecuteScalarAsync());
    }

    private static async Task EnsurePostgreSqlAvailableAsync()
    {
        var connectionString = GetConfiguredConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            const string message = "Configure AUTHCORE_TEST_POSTGRES para executar a validação real de telemetria Npgsql.";
            if (IsPostgreSqlRequired())
            {
                throw new InvalidOperationException(message);
            }

            throw new InvalidOperationException(message);
        }

        try
        {
            await using var dataSource = BuildDataSource(connectionString);
            await using var connection = await dataSource.OpenConnectionAsync();
        }
        catch (Exception exception)
        {
            var message = $"PostgreSQL de integração indisponível para telemetria Npgsql: {exception.GetType().Name}.";
            if (IsPostgreSqlRequired())
            {
                throw new InvalidOperationException(message, exception);
            }

            throw new InvalidOperationException(message);
        }
    }

    private static string GetRequiredConnectionString()
    {
        return GetConfiguredConnectionString()
            ?? throw new InvalidOperationException("Configure AUTHCORE_TEST_POSTGRES para executar a validação real de telemetria Npgsql.");
    }

    private static string? GetConfiguredConnectionString()
    {
        return Environment.GetEnvironmentVariable("AUTHCORE_TEST_POSTGRES");
    }

    private static bool IsPostgreSqlRequired()
    {
        var value = Environment.GetEnvironmentVariable("OBSERVABILITY_POSTGRES_REQUIRED");

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString)
        {
            Name = DataSourceName
        };

        return builder.Build();
    }

    private static IHost BuildObservabilityHost(
        CapturedTelemetry telemetry,
        string traceSamplingRatio,
        bool enabled = true)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development"
        });
        builder.Configuration.AddInMemoryCollection(CreateObservabilityConfiguration(traceSamplingRatio, enabled));
        builder.Services.AddObservability(
            builder.Configuration,
            builder.Environment,
            new ObservabilityServiceDescriptor(
                activitySourceNames: [ActivitySourceName],
                meterNames: [MeterName, DatabaseMetrics.MeterName],
                configureTracingProvider: tracing => tracing
                    .AddNpgsql()
                    .AddProcessor(new NpgsqlTelemetryActivityProcessor()),
                configureMeterProvider: metrics => metrics
                    .AddNpgsqlInstrumentation()
                    .AddView(instrument => instrument.Meter.Name == NpgsqlObservability.MeterName
                        ? NpgsqlObservability.CreateMetricView()
                        : null)));
        builder.Services.ConfigureOpenTelemetryTracerProvider((_, tracing) =>
            tracing.AddInMemoryExporter(telemetry.Activities));
        builder.Services.ConfigureOpenTelemetryMeterProvider((_, metrics) =>
            metrics.AddInMemoryExporter(telemetry.Metrics));

        return builder.Build();
    }

    private static IConfiguration CreateInfrastructureConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = CreateInfrastructureConnectionString(),
                ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false",
                ["Redis:KeyPrefix"] = "authcore:observability-tests"
            })
            .Build();
    }

    private static string CreateInfrastructureConnectionString()
    {
        return $"Host={HostSentinel};Port=5432;Database=postgres;Username={UsernameSentinel};Password={PasswordSentinel};Pooling=false";
    }

    private static IReadOnlyDictionary<string, string?> CreateObservabilityConfiguration(
        string traceSamplingRatio,
        bool enabled)
    {
        return new Dictionary<string, string?>
        {
            ["Observability:Enabled"] = enabled.ToString(),
            ["Observability:ServiceName"] = "authcore-api-tests",
            ["Observability:ServiceNamespace"] = "auth-core-microservices",
            ["Observability:OtlpEnabled"] = "false",
            ["Observability:ConsoleExporterEnabled"] = "false",
            ["Observability:TraceSamplingRatio"] = traceSamplingRatio,
            ["Observability:ExcludeHealthChecks"] = "true",
            ["Observability:RequestLogging:Enabled"] = "true",
            ["Observability:RequestLogging:SlowRequestThresholdMilliseconds"] = "1000"
        };
    }

    private static async Task ForceFlushAsync(IHost host)
    {
        FlushTelemetry(host);
        await Task.Delay(100);
    }

    private static async Task ForceFlushUntilAsync(IHost host, Func<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var timeout = DateTimeOffset.UtcNow.AddSeconds(5);
        do
        {
            FlushTelemetry(host);
            if (condition())
            {
                return;
            }

            await Task.Delay(100);
        }
        while (DateTimeOffset.UtcNow < timeout);

        await ForceFlushAsync(host);
    }

    private static void FlushTelemetry(IHost host)
    {
        host.Services.GetService<TracerProvider>()?.ForceFlush();
        host.Services.GetService<MeterProvider>()?.ForceFlush();
    }

    private static bool IsSuccessfulNpgsqlCommandSpan(Activity activity)
    {
        return activity.Source.Name == MeterName
            && activity.Kind == ActivityKind.Client
            && activity.Status != ActivityStatusCode.Error
            && RenderActivity(activity).Contains(DataSourceName, StringComparison.Ordinal);
    }

    private static bool IsFailedNpgsqlCommandSpan(Activity activity)
    {
        return activity.Source.Name == MeterName
            && activity.Kind == ActivityKind.Client
            && activity.Status == ActivityStatusCode.Error
            && RenderActivity(activity).Contains(DataSourceName, StringComparison.Ordinal);
    }

    private static bool IsNpgsqlMetric(Metric metric)
    {
        return metric.MeterName == MeterName;
    }

    private static bool ContainsOperationDurationMetric(IEnumerable<Metric> metrics)
    {
        return metrics.Any(metric => metric.Name == "db.client.operation.duration"
            && IsNpgsqlMetric(metric));
    }

    private static string RenderTelemetry(IEnumerable<Activity> activities, IEnumerable<Metric> metrics)
    {
        var renderedActivities = string.Join(Environment.NewLine, activities.Select(RenderActivity).Distinct());
        var renderedMetrics = string.Join(Environment.NewLine, metrics.Select(RenderMetric).Distinct());

        return $"{renderedActivities}{Environment.NewLine}{renderedMetrics}";
    }

    private static string RenderActivity(Activity activity)
    {
        var tags = string.Join(" ", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"));
        var events = string.Join(" ", activity.Events.Select(activityEvent =>
            $"{activityEvent.Name} {string.Join(" ", activityEvent.Tags.Select(tag => $"{tag.Key}={tag.Value}"))}"));

        return $"{activity.Source.Name} {activity.DisplayName} {activity.OperationName} {activity.Status} {tags} {events}";
    }

    private static string RenderMetric(Metric metric)
    {
        return $"{metric.MeterName} {metric.Name} {string.Join(" ", GetMetricTags(metric).Select(tag => $"{tag.Key}={tag.Value}"))}";
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> GetMetricTags(Metric metric)
    {
        var tags = new List<KeyValuePair<string, object?>>();

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            foreach (var tag in metricPoint.Tags)
            {
                tags.Add(new KeyValuePair<string, object?>(tag.Key, tag.Value));
            }
        }

        return tags;
    }

    private sealed class CapturedTelemetry
    {
        public List<Activity> Activities { get; } = [];

        public List<Metric> Metrics { get; } = [];
    }

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute(string connectionStringEnvironmentVariable)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringEnvironmentVariable);

            if (!IsPostgreSqlRequired()
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable)))
            {
                Skip = $"Configure {connectionStringEnvironmentVariable} para executar a validação real de telemetria Npgsql.";
            }
        }
    }

    private readonly record struct ParentActivityContext(ActivityTraceId TraceId, ActivitySpanId SpanId);
}
