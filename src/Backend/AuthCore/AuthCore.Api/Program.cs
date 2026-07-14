using AuthCore.Api;
using AuthCore.Api.Observability;
using AuthCore.Application;
using AuthCore.Infrastructure;
using AuthCore.Infrastructure.Observability;
using AuthCore.Infrastructure.Persistences.Migrations;
using AuthCore.Infrastructure.Services.Messaging;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddObservability(
    builder.Configuration,
    builder.Environment,
    new ObservabilityServiceDescriptor(activitySourceNames:
    [
        RedisTelemetry.ActivitySourceName,
        RabbitMqTelemetry.ActivitySourceName
    ],
    meterNames:
    [
        DatabaseMetrics.MeterName,
        NpgsqlObservability.MeterName,
        RedisTelemetry.MeterName,
        RabbitMqTelemetry.MeterName,
        OutboxMetrics.MeterName,
        AuthBusinessMetrics.MeterName,
        UnhandledExceptionMetrics.MeterName
    ],
    configureTracingProvider: tracing => tracing
        .AddNpgsql()
        .AddProcessor(new NpgsqlTelemetryActivityProcessor()),
    configureMeterProvider: metrics => metrics
        .AddNpgsqlInstrumentation()
        .AddView(instrument => instrument.Meter.Name == NpgsqlObservability.MeterName
            ? NpgsqlObservability.CreateMetricView()
            : null)));
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
app.UseAuthFlowTelemetry();
app.UseExceptionHandler();
app.UseCors("AuthCoreBrowserSession");
app.UseAuthentication();
app.UseAuthorization();
app.MapStandardHealthCheckEndpoints();
app.MapControllers();

app.Run();

/// <summary>
/// Representa o ponto de entrada da API.
/// </summary>
public partial class Program
{
}
