using NotificationCore.Api;
using NotificationCore.Api.Observability;
using NotificationCore.Application;
using NotificationCore.Infrastructure;
using NotificationCore.Infrastructure.Observability;
using NotificationCore.Infrastructure.Persistences.Migrations;
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
        RabbitMqTelemetry.ActivitySourceName,
        NotificationDispatchTelemetry.ActivitySourceName,
        SmtpTelemetry.ActivitySourceName
    ],
    meterNames:
    [
        DatabaseMetrics.MeterName,
        NpgsqlObservability.MeterName,
        RabbitMqTelemetry.MeterName,
        NotificationMetrics.MeterName,
        SmtpTelemetry.MeterName,
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
app.MapStandardHealthCheckEndpoints();
app.MapControllers();

app.Run();

/// <summary>
/// Representa o ponto de entrada da API de notificações.
/// </summary>
public partial class Program
{
}
