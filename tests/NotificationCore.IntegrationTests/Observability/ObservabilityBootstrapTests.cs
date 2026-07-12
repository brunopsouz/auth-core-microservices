using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Observability;

namespace NotificationCore.IntegrationTests.Observability;

public sealed class ObservabilityBootstrapTests
{
    [Fact]
    public async Task AddObservability_WhenOtlpIsDisabled_ShouldBuildHostWithConfiguredOptions()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(CreateConfiguration());
        builder.Services.AddObservability(builder.Configuration, builder.Environment);

        await using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.False(options.OtlpEnabled);
        Assert.Equal("notificationcore-api", options.ServiceName);
        Assert.Equal("auth-core-microservices", options.ServiceNamespace);
    }

    [Fact]
    public void AddObservability_WhenTraceSamplingRatioIsLessThanZero_ShouldFailFast()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(CreateConfiguration(traceSamplingRatio: "-0.1"));

        Assert.Throws<OptionsValidationException>(
            () => builder.Services.AddObservability(builder.Configuration, builder.Environment));
    }

    private static IReadOnlyDictionary<string, string?> CreateConfiguration(string traceSamplingRatio = "1")
    {
        return new Dictionary<string, string?>
        {
            ["Observability:Enabled"] = "true",
            ["Observability:ServiceName"] = "notificationcore-api",
            ["Observability:ServiceNamespace"] = "auth-core-microservices",
            ["Observability:OtlpEnabled"] = "false",
            ["Observability:ConsoleExporterEnabled"] = "false",
            ["Observability:TraceSamplingRatio"] = traceSamplingRatio,
            ["Observability:ExcludeHealthChecks"] = "true"
        };
    }
}
