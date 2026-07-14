using System.Diagnostics.Metrics;

namespace AuthCore.Api.Observability;

/// <summary>
/// Representa métricas técnicas dos fluxos de autenticação do AuthCore.
/// </summary>
internal sealed class AuthBusinessMetrics
{
    internal const string MeterName = "authcore.authentication";

    internal const string FlowPasswordSession = "password_session";
    internal const string FlowPasswordToken = "password_token";
    internal const string FlowGoogleSession = "google_session";

    internal const string OperationCreate = "create";
    internal const string OperationRotate = "rotate";
    internal const string OperationRevoke = "revoke";
    internal const string OperationRevokeAll = "revoke_all";

    internal const string ResultSuccess = "success";
    internal const string ResultFailure = "failure";
    internal const string ResultBlocked = "blocked";
    internal const string ResultCancelled = "cancelled";
    internal const string ResultNoop = "noop";

    internal const string ReasonNone = "none";
    internal const string ReasonInvalidCredentials = "invalid_credentials";
    internal const string ReasonUserInactive = "user_inactive";
    internal const string ReasonRateLimited = "rate_limited";
    internal const string ReasonProviderError = "provider_error";
    internal const string ReasonInvalidState = "invalid_state";
    internal const string ReasonValidationError = "validation_error";
    internal const string ReasonCancelled = "cancelled";
    internal const string ReasonInvalidToken = "invalid_token";
    internal const string ReasonSessionNotFound = "session_not_found";
    internal const string ReasonDuplicateEmail = "duplicate_email";
    internal const string ReasonPersistenceFailure = "persistence_failure";
    internal const string ReasonUnknown = "unknown";

    private const string FlowTagName = "flow";
    private const string OperationTagName = "operation";
    private const string ReasonTagName = "reason";
    private const string ResultTagName = "result";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> AuthenticationAttempts = Meter.CreateCounter<long>(
        "authcore.authentication.attempts",
        unit: "{attempt}",
        description: "Number of authentication attempts grouped by flow, result and normalized reason.");
    private static readonly Counter<long> RefreshTokenOperations = Meter.CreateCounter<long>(
        "authcore.refresh_tokens.operations",
        unit: "{operation}",
        description: "Number of refresh token operations grouped by operation, result and normalized reason.");
    private static readonly Counter<long> SessionOperations = Meter.CreateCounter<long>(
        "authcore.sessions.operations",
        unit: "{operation}",
        description: "Number of session operations grouped by operation, result and normalized reason.");
    private static readonly Counter<long> RegistrationAttempts = Meter.CreateCounter<long>(
        "authcore.registration.attempts",
        unit: "{attempt}",
        description: "Number of registration attempts grouped by result and normalized reason.");
    private static readonly Counter<long> EmailVerificationAttempts = Meter.CreateCounter<long>(
        "authcore.email_verification.attempts",
        unit: "{attempt}",
        description: "Number of email verification attempts grouped by result and normalized reason.");

    /// <summary>
    /// Operação para registrar tentativa de autenticação.
    /// </summary>
    /// <param name="flow">Fluxo normalizado da tentativa.</param>
    /// <param name="result">Resultado normalizado da tentativa.</param>
    /// <param name="reason">Motivo normalizado da tentativa.</param>
    public void RecordAuthenticationAttempt(string flow, string result, string reason)
    {
        AuthenticationAttempts.Add(
            1,
            new KeyValuePair<string, object?>(FlowTagName, NormalizeAuthenticationFlow(flow)),
            new KeyValuePair<string, object?>(ResultTagName, NormalizeAuthenticationResult(result)),
            new KeyValuePair<string, object?>(ReasonTagName, NormalizeAuthenticationReason(reason)));
    }

    /// <summary>
    /// Operação para registrar operação sobre refresh token.
    /// </summary>
    /// <param name="operation">Operação normalizada.</param>
    /// <param name="result">Resultado normalizado.</param>
    /// <param name="reason">Motivo normalizado.</param>
    public void RecordRefreshTokenOperation(string operation, string result, string reason)
    {
        RefreshTokenOperations.Add(
            1,
            new KeyValuePair<string, object?>(OperationTagName, NormalizeRefreshTokenOperation(operation)),
            new KeyValuePair<string, object?>(ResultTagName, NormalizeRefreshTokenResult(result)),
            new KeyValuePair<string, object?>(ReasonTagName, NormalizeRefreshTokenReason(reason)));
    }

    /// <summary>
    /// Operação para registrar operação sobre sessão.
    /// </summary>
    /// <param name="operation">Operação normalizada.</param>
    /// <param name="result">Resultado normalizado.</param>
    /// <param name="reason">Motivo normalizado.</param>
    public void RecordSessionOperation(string operation, string result, string reason)
    {
        SessionOperations.Add(
            1,
            new KeyValuePair<string, object?>(OperationTagName, NormalizeSessionOperation(operation)),
            new KeyValuePair<string, object?>(ResultTagName, NormalizeSessionResult(result)),
            new KeyValuePair<string, object?>(ReasonTagName, NormalizeSessionReason(reason)));
    }

    /// <summary>
    /// Operação para registrar tentativa de registro.
    /// </summary>
    /// <param name="result">Resultado normalizado.</param>
    /// <param name="reason">Motivo normalizado.</param>
    public void RecordRegistrationAttempt(string result, string reason)
    {
        RegistrationAttempts.Add(
            1,
            new KeyValuePair<string, object?>(ResultTagName, NormalizeRegistrationResult(result)),
            new KeyValuePair<string, object?>(ReasonTagName, NormalizeRegistrationReason(reason)));
    }

    /// <summary>
    /// Operação para registrar tentativa de verificação de e-mail.
    /// </summary>
    /// <param name="result">Resultado normalizado.</param>
    /// <param name="reason">Motivo normalizado.</param>
    public void RecordEmailVerificationAttempt(string result, string reason)
    {
        EmailVerificationAttempts.Add(
            1,
            new KeyValuePair<string, object?>(ResultTagName, NormalizeEmailVerificationResult(result)),
            new KeyValuePair<string, object?>(ReasonTagName, NormalizeEmailVerificationReason(reason)));
    }

    private static string NormalizeAuthenticationFlow(string flow)
    {
        return flow switch
        {
            FlowPasswordSession => FlowPasswordSession,
            FlowPasswordToken => FlowPasswordToken,
            FlowGoogleSession => FlowGoogleSession,
            _ => ReasonUnknown
        };
    }

    private static string NormalizeAuthenticationResult(string result)
    {
        return result switch
        {
            ResultSuccess => ResultSuccess,
            ResultFailure => ResultFailure,
            ResultBlocked => ResultBlocked,
            ResultCancelled => ResultCancelled,
            _ => ResultFailure
        };
    }

    private static string NormalizeAuthenticationReason(string reason)
    {
        return reason switch
        {
            ReasonNone => ReasonNone,
            ReasonInvalidCredentials => ReasonInvalidCredentials,
            ReasonUserInactive => ReasonUserInactive,
            "email_not_verified" => "email_not_verified",
            ReasonRateLimited => ReasonRateLimited,
            ReasonProviderError => ReasonProviderError,
            ReasonInvalidState => ReasonInvalidState,
            ReasonValidationError => ReasonValidationError,
            ReasonCancelled => ReasonCancelled,
            ReasonUnknown => ReasonUnknown,
            _ => ReasonUnknown
        };
    }

    private static string NormalizeRefreshTokenOperation(string operation)
    {
        return operation switch
        {
            OperationRotate => OperationRotate,
            OperationRevoke => OperationRevoke,
            OperationRevokeAll => OperationRevokeAll,
            _ => ReasonUnknown
        };
    }

    private static string NormalizeRefreshTokenResult(string result)
    {
        return result switch
        {
            ResultSuccess => ResultSuccess,
            ResultFailure => ResultFailure,
            ResultCancelled => ResultCancelled,
            _ => ResultFailure
        };
    }

    private static string NormalizeRefreshTokenReason(string reason)
    {
        return reason switch
        {
            ReasonNone => ReasonNone,
            ReasonInvalidToken => ReasonInvalidToken,
            "expired_token" => "expired_token",
            "revoked_token" => "revoked_token",
            "reused_token" => "reused_token",
            ReasonUserInactive => ReasonUserInactive,
            ReasonValidationError => ReasonValidationError,
            ReasonCancelled => ReasonCancelled,
            ReasonUnknown => ReasonUnknown,
            _ => ReasonUnknown
        };
    }

    private static string NormalizeSessionOperation(string operation)
    {
        return operation switch
        {
            OperationCreate => OperationCreate,
            OperationRevoke => OperationRevoke,
            OperationRevokeAll => OperationRevokeAll,
            _ => ReasonUnknown
        };
    }

    private static string NormalizeSessionResult(string result)
    {
        return result switch
        {
            ResultSuccess => ResultSuccess,
            ResultFailure => ResultFailure,
            ResultNoop => ResultNoop,
            ResultCancelled => ResultCancelled,
            _ => ResultFailure
        };
    }

    private static string NormalizeSessionReason(string reason)
    {
        return reason switch
        {
            ReasonNone => ReasonNone,
            ReasonSessionNotFound => ReasonSessionNotFound,
            "already_revoked" => "already_revoked",
            "storage_failure" => "storage_failure",
            ReasonCancelled => ReasonCancelled,
            ReasonUnknown => ReasonUnknown,
            _ => ReasonUnknown
        };
    }

    private static string NormalizeRegistrationResult(string result)
    {
        return result switch
        {
            ResultSuccess => ResultSuccess,
            ResultFailure => ResultFailure,
            ResultCancelled => ResultCancelled,
            _ => ResultFailure
        };
    }

    private static string NormalizeRegistrationReason(string reason)
    {
        return reason switch
        {
            ReasonNone => ReasonNone,
            ReasonDuplicateEmail => ReasonDuplicateEmail,
            ReasonValidationError => ReasonValidationError,
            ReasonPersistenceFailure => ReasonPersistenceFailure,
            ReasonCancelled => ReasonCancelled,
            ReasonUnknown => ReasonUnknown,
            _ => ReasonUnknown
        };
    }

    private static string NormalizeEmailVerificationResult(string result)
    {
        return result switch
        {
            ResultSuccess => ResultSuccess,
            ResultFailure => ResultFailure,
            ResultNoop => ResultNoop,
            ResultCancelled => ResultCancelled,
            _ => ResultFailure
        };
    }

    private static string NormalizeEmailVerificationReason(string reason)
    {
        return reason switch
        {
            ReasonNone => ReasonNone,
            ReasonInvalidToken => ReasonInvalidToken,
            "expired_token" => "expired_token",
            "already_verified" => "already_verified",
            "user_not_found" => "user_not_found",
            ReasonValidationError => ReasonValidationError,
            ReasonCancelled => ReasonCancelled,
            ReasonUnknown => ReasonUnknown,
            _ => ReasonUnknown
        };
    }
}
