using System.Diagnostics.Metrics;

namespace AuthCore.Api.Observability;

/// <summary>
/// Representa metricas do fluxo de autenticacao externa.
/// </summary>
internal sealed class ExternalAuthenticationMetrics
{
    internal const string MeterName = "authcore.auth.external";

    private const string ProviderTagName = "provider";
    private const string ReasonTagName = "reason";
    private const string ResultTagName = "result";
    private const string GoogleProvider = "google";
    private const string SuccessResult = "success";
    private const string PendingResult = "pending";
    private const string FailureResult = "failure";
    private const string CancelledResult = "cancelled";
    private const string ValidationReason = "validation";
    private const string CancelledReason = "cancelled";
    private const string UnknownReason = "unknown";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> StartedLogins = Meter.CreateCounter<long>(
        "authcore.auth.external.login.started",
        unit: "{request}",
        description: "Number of external authentication login flows started.");
    private static readonly Counter<long> SucceededLogins = Meter.CreateCounter<long>(
        "authcore.auth.external.login.succeeded",
        unit: "{request}",
        description: "Number of external authentication login flows completed successfully.");
    private static readonly Counter<long> FailedLogins = Meter.CreateCounter<long>(
        "authcore.auth.external.login.failed",
        unit: "{request}",
        description: "Number of external authentication login flows that failed.");
    private static readonly Counter<long> CancelledLogins = Meter.CreateCounter<long>(
        "authcore.auth.external.login.cancelled",
        unit: "{request}",
        description: "Number of external authentication login flows cancelled by the provider callback.");
    private static readonly Histogram<double> CallbackDuration = Meter.CreateHistogram<double>(
        "authcore.auth.external.callback.duration",
        unit: "s",
        description: "Duration of external authentication callback processing.");

    /// <summary>
    /// Operacao para registrar inicio de login Google.
    /// </summary>
    public void RecordGoogleLoginStarted()
    {
        StartedLogins.Add(1, new KeyValuePair<string, object?>(ProviderTagName, GoogleProvider));
    }

    /// <summary>
    /// Operacao para registrar sucesso no login Google.
    /// </summary>
    /// <param name="requiresOnboarding">Indica se o fluxo direcionou para onboarding.</param>
    public void RecordGoogleLoginSucceeded(bool requiresOnboarding)
    {
        SucceededLogins.Add(
            1,
            new KeyValuePair<string, object?>(ProviderTagName, GoogleProvider),
            new KeyValuePair<string, object?>(ResultTagName, requiresOnboarding ? PendingResult : SuccessResult));
    }

    /// <summary>
    /// Operacao para registrar falha no login Google.
    /// </summary>
    /// <param name="reason">Motivo normalizado da falha.</param>
    public void RecordGoogleLoginFailed(string reason)
    {
        FailedLogins.Add(
            1,
            new KeyValuePair<string, object?>(ProviderTagName, GoogleProvider),
            new KeyValuePair<string, object?>(ReasonTagName, NormalizeReason(reason)));
    }

    /// <summary>
    /// Operacao para registrar cancelamento ou callback externo invalido.
    /// </summary>
    public void RecordGoogleLoginCancelled()
    {
        CancelledLogins.Add(
            1,
            new KeyValuePair<string, object?>(ProviderTagName, GoogleProvider),
            new KeyValuePair<string, object?>(ResultTagName, CancelledResult));
    }

    /// <summary>
    /// Operacao para registrar duracao do callback Google.
    /// </summary>
    /// <param name="elapsed">Duracao do processamento.</param>
    /// <param name="result">Resultado normalizado do callback.</param>
    public void RecordGoogleCallbackDuration(TimeSpan elapsed, string result)
    {
        CallbackDuration.Record(
            ToNonNegativeSeconds(elapsed),
            new KeyValuePair<string, object?>(ProviderTagName, GoogleProvider),
            new KeyValuePair<string, object?>(ResultTagName, NormalizeResult(result)));
    }

    /// <summary>
    /// Operacao para normalizar motivo de falha.
    /// </summary>
    /// <param name="reason">Motivo informado.</param>
    /// <returns>Motivo seguro para metricas.</returns>
    private static string NormalizeReason(string reason)
    {
        return reason switch
        {
            "external_callback_failed" => CancelledReason,
            "missing_required_claims" => ValidationReason,
            _ => UnknownReason
        };
    }

    private static string NormalizeResult(string result)
    {
        return result switch
        {
            SuccessResult => SuccessResult,
            FailureResult => FailureResult,
            CancelledResult => CancelledResult,
            _ => UnknownReason
        };
    }

    private static double ToNonNegativeSeconds(TimeSpan elapsed)
    {
        return Math.Max(0, elapsed.TotalSeconds);
    }
}
