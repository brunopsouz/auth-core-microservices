using System.Diagnostics;
using AuthCore.Infrastructure;
using AuthCore.IntegrationTests.Passports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shared.Observability;
using DatabaseMetrics = AuthCore.Infrastructure.Observability.DatabaseMetrics;

namespace AuthCore.IntegrationTests.Observability;

public sealed class NpgsqlInstrumentationTests : IClassFixture<PostgreSqlIntegrationFixture>
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

    private readonly PostgreSqlIntegrationFixture _fixture;

    public NpgsqlInstrumentationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
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
        var configuredName = builder
            .GetType()
            .GetProperty("Name")?
            .GetValue(builder)?
            .ToString();

        Assert.NotNull(dataSource);
        Assert.Equal(DataSourceName, configuredName);
        Assert.DoesNotContain(HostSentinel, configuredName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(UsernameSentinel, configuredName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PasswordSentinel, configuredName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddObservability_WhenNpgsqlCommandRuns_ShouldExportSafeChildSpanAndNativeMetrics()
    {
        if (!_fixture.IsAvailable)
            return;

        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1");
        await host.StartAsync();

        await ExecuteSuccessfulCommandAsync();
        await ExecuteFailingCommandAsync();
        await ForceFlushAsync(host);

        var clientSpan = Assert.Single(telemetry.Activities.Where(IsSuccessfulNpgsqlCommandSpan));
        var errorSpan = Assert.Single(telemetry.Activities.Where(IsFailedNpgsqlCommandSpan));
        var renderedTelemetry = RenderTelemetry(telemetry);
        var operationMetric = Assert.Single(telemetry.Metrics.Where(metric => metric.Name == "db.client.operation.duration"));

        Assert.Equal(ActivityKind.Client, clientSpan.Kind);
        Assert.Equal(ActivityKind.Client, errorSpan.Kind);
        Assert.Equal(ActivityStatusCode.Error, errorSpan.Status);
        Assert.Equal(clientSpan.TraceId, clientSpan.Parent!.TraceId);
        Assert.NotEqual(clientSpan.SpanId, clientSpan.ParentSpanId);
        Assert.Equal("s", operationMetric.Unit);
        Assert.Contains(GetMetricTags(operationMetric), tag =>
            tag.Key.Contains("pool.name", StringComparison.Ordinal)
            && tag.Value?.ToString() == DataSourceName);
        Assert.Contains(telemetry.Metrics, metric => metric.Name.StartsWith("db.client.connection.", StringComparison.Ordinal));
        Assert.DoesNotContain("db.statement", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db.query.text", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"Host={HostSentinel}", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"Username={UsernameSentinel}", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"Password={PasswordSentinel}", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SqlSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ParameterSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EmailSentinel, renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT @value", renderedTelemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception.message", RenderActivity(errorSpan), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(telemetry.Metrics, metric => metric.MeterName == DatabaseMetrics.MeterName
            && metric.Name == "db.client.operation.duration");
    }

    [Fact]
    public async Task AddObservability_WhenSamplingIsZero_ShouldNotExportNpgsqlSpansButShouldExportMetrics()
    {
        if (!_fixture.IsAvailable)
            return;

        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "0");
        await host.StartAsync();

        await ExecuteSuccessfulCommandAsync();
        await ForceFlushAsync(host);

        Assert.DoesNotContain(telemetry.Activities, activity => activity.Source.Name == MeterName);
        Assert.Contains(telemetry.Metrics, metric => metric.Name == "db.client.operation.duration");
    }

    [Fact]
    public async Task AddObservability_WhenDisabled_ShouldNotCreateProvidersOrBlockDatabaseAccess()
    {
        if (!_fixture.IsAvailable)
            return;

        var telemetry = new CapturedTelemetry();
        using var host = BuildObservabilityHost(telemetry, traceSamplingRatio: "1", enabled: false);
        await host.StartAsync();

        await ExecuteSuccessfulCommandAsync();

        Assert.Null(host.Services.GetService<TracerProvider>());
        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.Empty(telemetry.Activities);
        Assert.Empty(telemetry.Metrics);
    }

    private async Task ExecuteSuccessfulCommandAsync()
    {
        using var activitySource = new ActivitySource(ActivitySourceName);
        using var parent = activitySource.StartActivity("npgsql-parent", ActivityKind.Internal);
        Assert.NotNull(parent);

        await using var dataSource = BuildDataSource(_fixture.DatabaseConnectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT @value::text /* {SqlSentinel} {EmailSentinel} */",
            connection);
        command.Parameters.AddWithValue("value", ParameterSentinel);

        Assert.Equal(ParameterSentinel, await command.ExecuteScalarAsync());
    }

    private async Task ExecuteFailingCommandAsync()
    {
        using var activitySource = new ActivitySource(ActivitySourceName);
        using var parent = activitySource.StartActivity("npgsql-parent-failure", ActivityKind.Internal);
        Assert.NotNull(parent);

        await using var dataSource = BuildDataSource(_fixture.DatabaseConnectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT 1 / @value", connection);
        command.Parameters.AddWithValue("value", 0);

        _ = await Assert.ThrowsAnyAsync<PostgresException>(() => command.ExecuteScalarAsync());
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
                configureTracingProvider: tracing => tracing.AddNpgsql(),
                configureMeterProvider: metrics => metrics.AddNpgsqlInstrumentation()));
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
        host.Services.GetService<TracerProvider>()?.ForceFlush();
        host.Services.GetService<MeterProvider>()?.ForceFlush();
        await Task.Delay(100);
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
            && activity.Status == ActivityStatusCode.Error;
    }

    private static string RenderTelemetry(CapturedTelemetry telemetry)
    {
        var activities = string.Join(Environment.NewLine, telemetry.Activities.Select(RenderActivity));
        var metrics = string.Join(Environment.NewLine, telemetry.Metrics.Select(RenderMetric));

        return $"{activities}{Environment.NewLine}{metrics}";
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
}
