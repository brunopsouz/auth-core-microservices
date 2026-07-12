using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Observability;

namespace Gateway.IntegrationTests.Observability;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task UseCorrelationId_WhenHeaderIsMissing_ShouldGenerateValidGuid()
    {
        await using var app = await StartApplicationAsync();
        using var httpClient = new HttpClient();

        using var response = await httpClient.GetAsync(GetAddress(app));

        var correlationId = GetCorrelationId(response);

        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task UseCorrelationId_WhenHeaderIsValid_ShouldPreserveValue()
    {
        const string expectedCorrelationId = "abc.DEF-123_456";

        await using var app = await StartApplicationAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, GetAddress(app));
        request.Headers.Add(CorrelationIdConstants.HeaderName, expectedCorrelationId);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(expectedCorrelationId, GetCorrelationId(response));
        Assert.Equal(expectedCorrelationId, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid value")]
    [InlineData("invalid/value")]
    public async Task UseCorrelationId_WhenHeaderIsInvalid_ShouldReplaceValue(string invalidCorrelationId)
    {
        await using var app = await StartApplicationAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, GetAddress(app));
        request.Headers.TryAddWithoutValidation(CorrelationIdConstants.HeaderName, invalidCorrelationId);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var correlationId = GetCorrelationId(response);

        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.NotEqual(invalidCorrelationId, correlationId);
    }

    [Fact]
    public async Task UseCorrelationId_WhenHeaderIsTooLong_ShouldReplaceValue()
    {
        var invalidCorrelationId = new string('a', 129);

        await using var app = await StartApplicationAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, GetAddress(app));
        request.Headers.Add(CorrelationIdConstants.HeaderName, invalidCorrelationId);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var correlationId = GetCorrelationId(response);

        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.NotEqual(invalidCorrelationId, correlationId);
    }

    [Fact]
    public async Task UseCorrelationId_WhenHeaderHasMultipleValues_ShouldReplaceValue()
    {
        await using var app = await StartApplicationAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, GetAddress(app));
        request.Headers.TryAddWithoutValidation(CorrelationIdConstants.HeaderName, ["first", "second"]);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        var correlationId = GetCorrelationId(response);

        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.NotEqual("first", correlationId);
        Assert.NotEqual("second", correlationId);
    }

    [Fact]
    public async Task UseCorrelationId_WhenRequestIsProcessed_ShouldStoreValueInHttpContext()
    {
        const string expectedCorrelationId = "context-123";

        await using var app = await StartApplicationAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, GetAddress(app));
        request.Headers.Add(CorrelationIdConstants.HeaderName, expectedCorrelationId);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(expectedCorrelationId, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UseCorrelationId_WhenEndpointLogs_ShouldKeepCorrelationIdInScope()
    {
        const string expectedCorrelationId = "scope-123";
        var loggerProvider = new CapturingLoggerProvider();

        await using var app = await StartApplicationAsync(loggerProvider);
        using var request = new HttpRequestMessage(HttpMethod.Get, GetAddress(app));
        request.Headers.Add(CorrelationIdConstants.HeaderName, expectedCorrelationId);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
        Assert.Contains(expectedCorrelationId, loggerProvider.CorrelationIds);
    }

    [Fact]
    public async Task UseCorrelationId_WhenActivityExists_ShouldTagActivityWithoutReplacingTraceId()
    {
        const string expectedCorrelationId = "activity-123";

        await using var app = await StartApplicationAsync(configureBeforeCorrelation: application =>
        {
            application.Use(async (context, next) =>
            {
                using var activity = new Activity("test-request");
                activity.Start();
                context.Items["TraceIdBefore"] = activity.TraceId.ToString();

                await next();
            });
        }, configureEndpoint: application =>
        {
            application.MapGet("/", (HttpContext context) =>
            {
                var traceIdBefore = (string)context.Items["TraceIdBefore"]!;
                var traceIdAfter = Activity.Current?.TraceId.ToString();
                var correlationId = Activity.Current
                    ?.GetTagItem(CorrelationIdConstants.ActivityTagName)
                    ?.ToString();

                return Results.Text($"{correlationId}|{traceIdBefore}|{traceIdAfter}");
            });
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, GetAddress(app));
        request.Headers.Add(CorrelationIdConstants.HeaderName, expectedCorrelationId);
        using var httpClient = new HttpClient();

        using var response = await httpClient.SendAsync(request);
        var parts = (await response.Content.ReadAsStringAsync()).Split('|');

        Assert.Equal(expectedCorrelationId, parts[0]);
        Assert.Equal(parts[1], parts[2]);
        Assert.NotEqual(expectedCorrelationId, parts[2]);
    }

    private static async Task<WebApplication> StartApplicationAsync(
        CapturingLoggerProvider? loggerProvider = null,
        Action<WebApplication>? configureBeforeCorrelation = null,
        Action<WebApplication>? configureEndpoint = null)
    {
        var builder = WebApplication.CreateBuilder();

        if (loggerProvider is not null)
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(loggerProvider);
        }

        var app = builder.Build();

        configureBeforeCorrelation?.Invoke(app);
        app.UseCorrelationId();
        if (configureEndpoint is null)
        {
            app.MapGet("/", (HttpContext context, ILoggerFactory loggerFactory) =>
            {
                loggerFactory.CreateLogger("CorrelationIdMiddlewareTests").LogInformation("Request handled.");

                return Results.Text((string)context.Items[CorrelationIdConstants.HttpContextItemKey]!);
            });
        }
        else
        {
            configureEndpoint(app);
        }

        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        return app;
    }

    private static string GetAddress(WebApplication app)
    {
        return app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();
    }

    private static string GetCorrelationId(HttpResponseMessage response)
    {
        return response.Headers.GetValues(CorrelationIdConstants.HeaderName).Single();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

        public List<string> CorrelationIds { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(this);
        }

        public void Dispose()
        {
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        private void CaptureScopes()
        {
            _scopeProvider.ForEachScope((scope, state) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> values)
                {
                    var correlationId = values
                        .FirstOrDefault(value => value.Key == CorrelationIdConstants.LogScopePropertyName)
                        .Value
                        ?.ToString();

                    if (!string.IsNullOrWhiteSpace(correlationId))
                    {
                        state.Add(correlationId);
                    }
                }
            }, CorrelationIds);
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerProvider _provider;

            public CapturingLogger(CapturingLoggerProvider provider)
            {
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return NullScope.Instance;
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
                _provider.CaptureScopes();
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
