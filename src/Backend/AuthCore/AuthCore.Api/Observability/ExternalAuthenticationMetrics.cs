using System.Diagnostics.Metrics;

namespace AuthCore.Api.Observability;

/// <summary>
/// Representa metricas do fluxo de autenticacao externa.
/// </summary>
public sealed class ExternalAuthenticationMetrics
{
    private static readonly Meter Meter = new("AuthCore.ExternalAuthentication", "1.0.0");
    private static readonly Counter<long> StartedLogins = Meter.CreateCounter<long>(
        "auth_google_login_started_total");
    private static readonly Counter<long> SucceededLogins = Meter.CreateCounter<long>(
        "auth_google_login_succeeded_total");
    private static readonly Counter<long> FailedLogins = Meter.CreateCounter<long>(
        "auth_google_login_failed_total");
    private static readonly Counter<long> CancelledLogins = Meter.CreateCounter<long>(
        "auth_google_login_cancelled_total");
    private static readonly Histogram<double> CallbackDuration = Meter.CreateHistogram<double>(
        "auth_google_callback_duration_ms");

    /// <summary>
    /// Operacao para registrar inicio de login Google.
    /// </summary>
    public void RecordGoogleLoginStarted()
    {
        StartedLogins.Add(1);
    }

    /// <summary>
    /// Operacao para registrar sucesso no login Google.
    /// </summary>
    /// <param name="requiresOnboarding">Indica se o fluxo direcionou para onboarding.</param>
    public void RecordGoogleLoginSucceeded(bool requiresOnboarding)
    {
        SucceededLogins.Add(
            1,
            new KeyValuePair<string, object?>("requires_onboarding", requiresOnboarding));
    }

    /// <summary>
    /// Operacao para registrar falha no login Google.
    /// </summary>
    /// <param name="reason">Motivo normalizado da falha.</param>
    public void RecordGoogleLoginFailed(string reason)
    {
        FailedLogins.Add(1, new KeyValuePair<string, object?>("reason", NormalizeReason(reason)));
    }

    /// <summary>
    /// Operacao para registrar cancelamento ou callback externo invalido.
    /// </summary>
    public void RecordGoogleLoginCancelled()
    {
        CancelledLogins.Add(1);
    }

    /// <summary>
    /// Operacao para registrar duracao do callback Google.
    /// </summary>
    /// <param name="elapsed">Duracao do processamento.</param>
    public void RecordGoogleCallbackDuration(TimeSpan elapsed)
    {
        CallbackDuration.Record(elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Operacao para normalizar motivo de falha.
    /// </summary>
    /// <param name="reason">Motivo informado.</param>
    /// <returns>Motivo seguro para metricas.</returns>
    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "unknown"
            : reason.Trim().ToLowerInvariant();
    }
}
