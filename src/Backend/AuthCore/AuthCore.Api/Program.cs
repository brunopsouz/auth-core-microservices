using AuthCore.Api;
using AuthCore.Api.Observability;
using AuthCore.Application;
using AuthCore.Infrastructure;
using AuthCore.Infrastructure.Observability;
using AuthCore.Infrastructure.Persistences.Migrations;
using AuthCore.Infrastructure.Services.Messaging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddObservability(
    builder.Configuration,
    builder.Environment,
    new ObservabilityServiceDescriptor(meterNames:
    [
        DatabaseMetrics.MeterName,
        NpgsqlObservability.MeterName,
        OutboxMetrics.MeterName,
        ExternalAuthenticationMetrics.MeterName,
        UnhandledExceptionMetrics.MeterName
    ],
    configureTracingProvider: tracing => tracing.AddNpgsql(),
    configureMeterProvider: metrics => metrics.AddNpgsqlInstrumentation()));
builder.Services.AddApi(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApplication();

var app = builder.Build();

await app.Services.ApplyInfrastructureMigrationsAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Redirect(app.Environment.IsDevelopment() ? "/swagger" : "/health"))
    .ExcludeFromDescription();

app.UseForwardedHeaders();
app.UseCorrelationId();
app.UseRouting();
app.UseRequestLogging();
app.UseExceptionHandler();
app.UseCors("AuthCoreBrowserSession");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    AllowCachingResponses = false,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    }
});
app.MapControllers();

app.Run();

/// <summary>
/// Representa o ponto de entrada da API.
/// </summary>
public partial class Program
{
}
