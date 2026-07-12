using NotificationCore.Api;
using NotificationCore.Application;
using NotificationCore.Infrastructure;
using NotificationCore.Infrastructure.Persistences.Migrations;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddObservability(
    builder.Configuration,
    builder.Environment,
    new ObservabilityServiceDescriptor(meterNames:
    [
        "NotificationCore.Database",
        "NotificationCore.Notifications"
    ]));
builder.Services.AddApi(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

await app.Services.ApplyInfrastructureMigrationsAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Redirect(app.Environment.IsDevelopment() ? "/swagger" : "/health"))
    .ExcludeFromDescription();

app.UseCorrelationId();
app.UseRouting();
app.UseRequestLogging();
app.UseExceptionHandler();
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
/// Representa o ponto de entrada da API de notificações.
/// </summary>
public partial class Program
{
}
