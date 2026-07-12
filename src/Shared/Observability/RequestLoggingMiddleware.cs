using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Observability;

/// <summary>
/// Representa middleware para registrar conclusão segura de requisições HTTP.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private static readonly EventId RequestCompletedEvent = new(1000, "HttpRequestCompleted");

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly ObservabilityOptions _options;
    private readonly RequestLoggingOptions _requestLoggingOptions;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="next">Próximo middleware do pipeline.</param>
    /// <param name="logger">Logger usado para registrar a conclusão.</param>
    /// <param name="options">Opções de observabilidade do serviço.</param>
    /// <param name="environment">Ambiente de execução atual.</param>
    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IOptions<ObservabilityOptions> options,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        _next = next;
        _logger = logger;
        _options = options.Value;
        _requestLoggingOptions = _options.RequestLogging;
        _environment = environment;
    }

    /// <summary>
    /// Operação para registrar a conclusão da requisição atual.
    /// </summary>
    /// <param name="context">Contexto HTTP atual.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = Stopwatch.GetTimestamp();
        Exception? exception = null;

        try
        {
            await _next(context);
        }
        catch (Exception caughtException)
        {
            exception = caughtException;
            throw;
        }
        finally
        {
            LogRequestCompleted(context, startedAt, exception);
        }
    }

    private void LogRequestCompleted(HttpContext context, long startedAt, Exception? exception)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        var statusCode = exception is null
            ? context.Response.StatusCode
            : StatusCodes.Status500InternalServerError;
        var route = ResolveRoute(context);
        var isSlowRequest = elapsed.TotalMilliseconds >= _requestLoggingOptions.SlowRequestThresholdMilliseconds;

        if (!ShouldLogRequest(route, statusCode, isSlowRequest))
        {
            return;
        }

        var errorCategory = ResolveErrorCategory(context, statusCode);
        var activity = Activity.Current;
        var state = CreateLogState(
            context,
            route,
            statusCode,
            elapsed.TotalMilliseconds,
            isSlowRequest,
            errorCategory,
            activity);

        _logger.Log(
            DetermineLogLevel(route, statusCode, isSlowRequest),
            RequestCompletedEvent,
            state,
            exception: null,
            static (logState, _) => logState.ToString());
    }

    private RequestCompletionLogState CreateLogState(
        HttpContext context,
        string route,
        int statusCode,
        double elapsedMilliseconds,
        bool isSlowRequest,
        string? errorCategory,
        Activity? activity)
    {
        var values = new List<KeyValuePair<string, object?>>
        {
            new("ServiceName", _options.ServiceName),
            new("EnvironmentName", _environment.EnvironmentName),
            new("CorrelationId", GetCorrelationId(context)),
            new("TraceId", activity?.TraceId.ToString()),
            new("SpanId", activity?.SpanId.ToString()),
            new("HttpMethod", context.Request.Method),
            new("Route", route),
            new("StatusCode", statusCode),
            new("ElapsedMilliseconds", elapsedMilliseconds),
            new("ErrorCategory", errorCategory),
            new("IsSlowRequest", isSlowRequest)
        };

        var userId = _requestLoggingOptions.IncludeUserId
            ? GetAuthenticatedUserId(context)
            : null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            values.Add(new("UserId", userId));
        }

        values.Add(new("{OriginalFormat}", "HTTP request completed."));

        return new RequestCompletionLogState(values);
    }

    private static string? GetCorrelationId(HttpContext context)
    {
        return context.Items.TryGetValue(CorrelationIdConstants.HttpContextItemKey, out var value)
            && value is string correlationId
                ? correlationId
                : null;
    }

    private static string? GetAuthenticatedUserId(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        return IsStableUserIdentifier(userId)
            ? userId
            : null;
    }

    private bool ShouldLogRequest(string route, int statusCode, bool isSlowRequest)
    {
        if (!_requestLoggingOptions.Enabled)
        {
            return false;
        }

        if (IsHealthCheck(route) && statusCode < StatusCodes.Status500InternalServerError)
        {
            return _requestLoggingOptions.LogHealthChecks;
        }

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            return _requestLoggingOptions.LogServerErrors;
        }

        if (isSlowRequest)
        {
            return true;
        }

        if (statusCode == StatusCodes.Status429TooManyRequests)
        {
            return _requestLoggingOptions.LogRateLimitedRequests;
        }

        if (statusCode is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices)
        {
            return _requestLoggingOptions.LogSuccessfulRequests;
        }

        if (statusCode is >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError)
        {
            return _requestLoggingOptions.LogClientErrors;
        }

        return false;
    }

    private static string ResolveRoute(HttpContext context)
    {
        if (context.GetEndpoint() is RouteEndpoint routeEndpoint
            && !string.IsNullOrWhiteSpace(routeEndpoint.RoutePattern.RawText))
        {
            return NormalizeRoute(routeEndpoint.RoutePattern.RawText);
        }

        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            return "/health";
        }

        return context.Response.StatusCode == StatusCodes.Status404NotFound
            ? RequestLoggingConstants.UnmatchedRoute
            : RequestLoggingConstants.OcelotDownstreamRoute;
    }

    private static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return RequestLoggingConstants.UnmatchedRoute;
        }

        return route.StartsWith("/", StringComparison.Ordinal)
            ? route
            : $"/{route}";
    }

    private static string? ResolveErrorCategory(HttpContext context, int statusCode)
    {
        if (context.Items.TryGetValue(RequestLoggingConstants.ErrorCategoryItemKey, out var value)
            && value is string errorCategory
            && !string.IsNullOrWhiteSpace(errorCategory))
        {
            return errorCategory;
        }

        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "validation",
            StatusCodes.Status401Unauthorized => "authentication",
            StatusCodes.Status403Forbidden => "authorization",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status409Conflict => "conflict",
            StatusCodes.Status422UnprocessableEntity => "validation",
            StatusCodes.Status429TooManyRequests => "rate_limit",
            >= StatusCodes.Status500InternalServerError => "unexpected",
            _ => null
        };
    }

    private static LogLevel DetermineLogLevel(string route, int statusCode, bool isSlowRequest)
    {
        if (IsHealthCheck(route))
        {
            return LogLevel.Debug;
        }

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogLevel.Error;
        }

        if (statusCode == StatusCodes.Status429TooManyRequests || isSlowRequest)
        {
            return LogLevel.Warning;
        }

        return statusCode is >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError
            ? LogLevel.Information
            : LogLevel.Information;
    }

    private static bool IsHealthCheck(string route)
    {
        return route.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || route.StartsWith("/health/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStableUserIdentifier(string? userId)
    {
        return !string.IsNullOrWhiteSpace(userId)
            && !userId.Contains('@', StringComparison.Ordinal);
    }

    private sealed class RequestCompletionLogState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly IReadOnlyList<KeyValuePair<string, object?>> _values;

        public RequestCompletionLogState(IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            _values = values;
        }

        public int Count => _values.Count;

        public KeyValuePair<string, object?> this[int index] => _values[index];

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return "HTTP request completed.";
        }
    }
}
