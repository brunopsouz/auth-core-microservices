using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shared.Observability;

/// <summary>
/// Representa writer seguro para respostas de health check.
/// </summary>
public static class SafeHealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Operação para escrever resposta mínima.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP.</param>
    /// <param name="report">Relatório de saúde.</param>
    public static Task WriteMinimalAsync(HttpContext httpContext, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(report);

        httpContext.Response.ContentType = "application/json";

        return JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            new MinimalHealthResponse(report.Status.ToString()),
            JsonOptions,
            httpContext.RequestAborted);
    }

    /// <summary>
    /// Operação para escrever resposta segura com dependências.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP.</param>
    /// <param name="report">Relatório de saúde.</param>
    public static Task WriteDependenciesAsync(HttpContext httpContext, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(report);

        httpContext.Response.ContentType = "application/json";

        var checks = report.Entries
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new DependencyHealthResponse(
                entry.Key,
                entry.Value.Status.ToString(),
                (long)Math.Round(entry.Value.Duration.TotalMilliseconds, MidpointRounding.AwayFromZero)))
            .ToArray();

        return JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            new DependencyHealthReportResponse(report.Status.ToString(), checks),
            JsonOptions,
            httpContext.RequestAborted);
    }

    private sealed record MinimalHealthResponse(string Status);

    private sealed record DependencyHealthReportResponse(
        string Status,
        IReadOnlyCollection<DependencyHealthResponse> Checks);

    private sealed record DependencyHealthResponse(
        string Name,
        string Status,
        long DurationMs);
}
