using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;

namespace NotificationCore.Infrastructure.Observability;

/// <summary>
/// Representa telemetria SMTP do NotificationCore.
/// </summary>
internal static class SmtpTelemetry
{
    internal const string ActivitySourceName = "notificationcore.smtp";
    internal const string MeterName = "notificationcore.smtp";

    private const string Provider = "smtp";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Histogram<double> SendDuration = Meter.CreateHistogram<double>(
        "notificationcore.smtp.send.duration",
        unit: "s",
        description: "Duration of SMTP send attempts performed by NotificationCore.");
    private static readonly Counter<long> SendAttempts = Meter.CreateCounter<long>(
        "notificationcore.smtp.send.attempts",
        unit: "{attempt}",
        description: "Number of SMTP send attempts performed by NotificationCore.");

    /// <summary>
    /// Operação para iniciar span de envio SMTP.
    /// </summary>
    /// <param name="host">Servidor SMTP configurado.</param>
    /// <param name="port">Porta SMTP configurada.</param>
    /// <returns>Activity criada ou nula.</returns>
    public static Activity? StartSendActivity(string host, int port)
    {
        var activity = ActivitySource.StartActivity("smtp send", ActivityKind.Client);

        activity?.SetTag("server.address", host);
        activity?.SetTag("server.port", port);
        activity?.SetTag("notification.provider", Provider);

        return activity;
    }

    /// <summary>
    /// Operação para concluir telemetria de tentativa SMTP.
    /// </summary>
    /// <param name="activity">Activity SMTP.</param>
    /// <param name="elapsed">Duração da tentativa.</param>
    /// <param name="result">Resultado controlado.</param>
    /// <param name="exception">Exceção capturada, quando houver.</param>
    public static void CompleteSend(
        Activity? activity,
        TimeSpan elapsed,
        string result,
        Exception? exception = null)
    {
        var normalizedResult = NormalizeResult(result);

        activity?.SetTag("notification.result", normalizedResult);

        if (exception is not null)
        {
            activity?.SetTag("error.type", MapErrorType(exception));
            activity?.SetStatus(ActivityStatusCode.Error);
        }

        SendDuration.Record(
            Math.Max(0, elapsed.TotalSeconds),
            new KeyValuePair<string, object?>("provider", Provider),
            new KeyValuePair<string, object?>("result", normalizedResult));
        SendAttempts.Add(
            1,
            new KeyValuePair<string, object?>("provider", Provider),
            new KeyValuePair<string, object?>("result", normalizedResult));
    }

    internal static string MapErrorType(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "cancelled",
            TimeoutException => "timeout",
            IOException or SocketException => "connectivity",
            MailKit.Security.AuthenticationException or System.Security.Authentication.AuthenticationException => "authentication",
            SmtpProtocolException or SmtpCommandException => "protocol",
            _ => "unknown"
        };
    }

    private static string NormalizeResult(string result)
    {
        return result switch
        {
            "success" => "success",
            "cancelled" => "cancelled",
            _ => "failure"
        };
    }
}
