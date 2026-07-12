using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Shared.Observability;

/// <summary>
/// Representa extensões para configurar a observabilidade transversal do host.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Operação para registrar opções, resource e providers de observabilidade.
    /// </summary>
    /// <param name="services">Coleção de serviços do host.</param>
    /// <param name="configuration">Configuração do host.</param>
    /// <param name="environment">Ambiente de execução do host.</param>
    /// <param name="descriptor">Fontes específicas que o host disponibiliza.</param>
    /// <returns>A coleção de serviços configurada.</returns>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ObservabilityServiceDescriptor? descriptor = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var options = BindAndValidateOptions(configuration);

        services
            .AddOptions<ObservabilityOptions>()
            .BindConfiguration(ObservabilityConstants.ConfigurationSectionName)
            .Validate(ValidateOptions, "A configuração de observabilidade é inválida.")
            .ValidateOnStart();

        if (!options.Enabled)
        {
            return services;
        }

        var serviceDescriptor = descriptor ?? new ObservabilityServiceDescriptor();
        var resourceBuilder = CreateResourceBuilder(options, environment);
        var sampler = CreateSampler(options, environment);
        var otlpEndpoint = ResolveOtlpEndpoint(options);

        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                options.ServiceName,
                serviceNamespace: options.ServiceNamespace,
                serviceVersion: GetServiceVersion()))
            .WithTracing(tracing => ConfigureTracing(
                tracing,
                serviceDescriptor,
                sampler,
                options,
                otlpEndpoint,
                resourceBuilder))
            .WithMetrics(metrics => ConfigureMetrics(
                metrics,
                serviceDescriptor,
                options,
                otlpEndpoint,
                resourceBuilder));

        services.AddLogging(logging => ConfigureLogging(logging, options, otlpEndpoint, resourceBuilder));

        return services;
    }

    private static void ConfigureTracing(
        TracerProviderBuilder tracing,
        ObservabilityServiceDescriptor descriptor,
        Sampler sampler,
        ObservabilityOptions options,
        Uri? otlpEndpoint,
        ResourceBuilder resourceBuilder)
    {
        tracing
            .SetResourceBuilder(resourceBuilder)
            .SetSampler(sampler)
            .AddAspNetCoreInstrumentation(instrumentation =>
            {
                instrumentation.Filter = context => ShouldTraceRequest(context.Request.Path, options);
                instrumentation.RecordException = false;
            })
            .AddHttpClientInstrumentation()
            .AddProcessor(new SafeHttpTelemetryActivityProcessor());

        foreach (var activitySourceName in descriptor.ActivitySourceNames)
        {
            tracing.AddSource(activitySourceName);
        }

        descriptor.ConfigureTracingProvider?.Invoke(tracing);

        if (options.OtlpEnabled)
        {
            tracing.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint!);
        }

        if (options.ConsoleExporterEnabled)
        {
            tracing.AddConsoleExporter();
        }
    }

    private static void ConfigureMetrics(
        MeterProviderBuilder metrics,
        ObservabilityServiceDescriptor descriptor,
        ObservabilityOptions options,
        Uri? otlpEndpoint,
        ResourceBuilder resourceBuilder)
    {
        metrics
            .SetResourceBuilder(resourceBuilder)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        foreach (var meterName in descriptor.MeterNames)
        {
            metrics.AddMeter(meterName);
        }

        descriptor.ConfigureMeterProvider?.Invoke(metrics);

        if (options.OtlpEnabled)
        {
            metrics.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint!);
        }

        if (options.ConsoleExporterEnabled)
        {
            metrics.AddConsoleExporter();
        }
    }

    private static bool ShouldTraceRequest(PathString path, ObservabilityOptions options)
    {
        return !options.ExcludeHealthChecks
            || !IsHealthCheckPath(path);
    }

    private static bool IsHealthCheckPath(PathString path)
    {
        return path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
    }

    private static void ConfigureLogging(
        ILoggingBuilder logging,
        ObservabilityOptions options,
        Uri? otlpEndpoint,
        ResourceBuilder resourceBuilder)
    {
        logging.AddOpenTelemetry(openTelemetry =>
        {
            openTelemetry.SetResourceBuilder(resourceBuilder);
            openTelemetry.IncludeFormattedMessage = true;
            openTelemetry.IncludeScopes = true;
            openTelemetry.ParseStateValues = true;

            if (options.OtlpEnabled)
            {
                openTelemetry.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint!);
            }
        });
    }

    private static ResourceBuilder CreateResourceBuilder(ObservabilityOptions options, IHostEnvironment environment)
    {
        return ResourceBuilder.CreateDefault()
            .AddService(
                options.ServiceName,
                serviceNamespace: options.ServiceNamespace,
                serviceVersion: GetServiceVersion())
            .AddAttributes([
                new KeyValuePair<string, object>("deployment.environment.name", environment.EnvironmentName)
            ]);
    }

    private static Sampler CreateSampler(ObservabilityOptions options, IHostEnvironment environment)
    {
        if (environment.IsDevelopment() && options.TraceSamplingRatio == 1d)
        {
            return new ParentBasedSampler(new AlwaysOnSampler());
        }

        return new ParentBasedSampler(new TraceIdRatioBasedSampler(options.TraceSamplingRatio));
    }

    private static ObservabilityOptions BindAndValidateOptions(IConfiguration configuration)
    {
        var options = configuration
            .GetSection(ObservabilityConstants.ConfigurationSectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        if (!ValidateOptions(options))
        {
            throw new OptionsValidationException(
                ObservabilityConstants.ConfigurationSectionName,
                typeof(ObservabilityOptions),
                ["A configuração de observabilidade é inválida."]);
        }

        return options;
    }

    private static bool ValidateOptions(ObservabilityOptions options)
    {
        if (options.RequestLogging.SlowRequestThresholdMilliseconds <= 0d)
        {
            return false;
        }

        if (!options.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.ServiceName)
            || string.IsNullOrWhiteSpace(options.ServiceNamespace)
            || !double.IsFinite(options.TraceSamplingRatio)
            || options.TraceSamplingRatio is < 0d or > 1d)
        {
            return false;
        }

        return !options.OtlpEnabled || ResolveOtlpEndpoint(options) is not null;
    }

    private static Uri? ResolveOtlpEndpoint(ObservabilityOptions options)
    {
        var endpoint = string.IsNullOrWhiteSpace(options.OtlpEndpoint)
            ? Environment.GetEnvironmentVariable(ObservabilityConstants.OtlpEndpointEnvironmentVariableName)
            : options.OtlpEndpoint;

        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }

    private static string? GetServiceVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
    }
}
