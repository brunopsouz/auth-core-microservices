using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shared.Observability;

/// <summary>
/// Define operações para mapear endpoints padronizados de health check.
/// </summary>
public static class HealthCheckEndpointExtensions
{
    /// <summary>
    /// Operação para mapear liveness, readiness, dependências e alias legado.
    /// </summary>
    /// <param name="app">Aplicação web.</param>
    /// <returns>Aplicação web atualizada.</returns>
    public static WebApplication MapStandardHealthCheckEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        MapHealthCheck(app, "/health/live", HasTag(HealthCheckTags.Live), SafeHealthCheckResponseWriter.WriteMinimalAsync);
        MapHealthCheck(app, "/health/ready", HasTag(HealthCheckTags.Ready), SafeHealthCheckResponseWriter.WriteMinimalAsync);
        MapHealthCheck(app, "/health/dependencies", HasTag(HealthCheckTags.Dependency), SafeHealthCheckResponseWriter.WriteDependenciesAsync);
        MapHealthCheck(app, "/health", HasTag(HealthCheckTags.Ready), SafeHealthCheckResponseWriter.WriteMinimalAsync);

        return app;
    }

    private static void MapHealthCheck(
        WebApplication app,
        string pattern,
        Func<HealthCheckRegistration, bool> predicate,
        Func<HttpContext, HealthReport, Task> responseWriter)
    {
        app.MapHealthChecks(pattern, new HealthCheckOptions
        {
            AllowCachingResponses = false,
            Predicate = predicate,
            ResponseWriter = responseWriter,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        })
        .AllowAnonymous();
    }

    private static Func<HealthCheckRegistration, bool> HasTag(string tag)
    {
        return registration => registration.Tags.Contains(tag, StringComparer.Ordinal);
    }
}
