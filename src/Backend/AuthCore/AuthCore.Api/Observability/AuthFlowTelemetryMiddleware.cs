namespace AuthCore.Api.Observability;

/// <summary>
/// Representa middleware para consolidar métricas de autenticação por rota HTTP.
/// </summary>
internal sealed class AuthFlowTelemetryMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="next">Próximo middleware do pipeline.</param>
    public AuthFlowTelemetryMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);

        _next = next;
    }

    /// <summary>
    /// Operação para processar a requisição atual.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP.</param>
    /// <param name="metrics">Métricas de autenticação do AuthCore.</param>
    public async Task InvokeAsync(HttpContext httpContext, AuthBusinessMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(metrics);

        var metricEvent = ResolveMetricEvent(httpContext.Request);

        try
        {
            await _next(httpContext);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            metricEvent?.RecordCancelled(metrics);
            throw;
        }

        metricEvent?.Record(metrics, httpContext.Response.StatusCode);
    }

    private static AuthMetricEvent? ResolveMetricEvent(HttpRequest request)
    {
        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/session/login", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.Authentication(AuthBusinessMetrics.FlowPasswordSession, recordSessionCreate: true);
        }

        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/token/login", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.Authentication(AuthBusinessMetrics.FlowPasswordToken, recordSessionCreate: false);
        }

        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/token/refresh", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.RefreshToken(AuthBusinessMetrics.OperationRotate);
        }

        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/token/logout", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.RefreshToken(AuthBusinessMetrics.OperationRevoke);
        }

        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/session/logout", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.Session(AuthBusinessMetrics.OperationRevoke);
        }

        if (request.Method.Equals(HttpMethods.Delete, StringComparison.Ordinal)
            && request.Path.StartsWithSegments("/api/auth/session/sessions", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.Session(AuthBusinessMetrics.OperationRevoke);
        }

        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/session/logout-all", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.Session(AuthBusinessMetrics.OperationRevokeAll);
        }

        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.Registration();
        }

        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/verify-email", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.EmailVerification();
        }

        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/complete-registration", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.EmailVerification();
        }

        if (request.Method.Equals(HttpMethods.Post, StringComparison.Ordinal)
            && request.Path.Equals("/api/auth/external/google/onboarding", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMetricEvent.Authentication(AuthBusinessMetrics.FlowGoogleSession, recordSessionCreate: true);
        }

        return null;
    }

    private sealed record AuthMetricEvent(AuthMetricKind Kind, string? Value, bool RecordSessionCreate)
    {
        public static AuthMetricEvent Authentication(string flow, bool recordSessionCreate)
        {
            return new AuthMetricEvent(AuthMetricKind.Authentication, flow, recordSessionCreate);
        }

        public static AuthMetricEvent RefreshToken(string operation)
        {
            return new AuthMetricEvent(AuthMetricKind.RefreshToken, operation, RecordSessionCreate: false);
        }

        public static AuthMetricEvent Session(string operation)
        {
            return new AuthMetricEvent(AuthMetricKind.Session, operation, RecordSessionCreate: false);
        }

        public static AuthMetricEvent Registration()
        {
            return new AuthMetricEvent(AuthMetricKind.Registration, Value: null, RecordSessionCreate: false);
        }

        public static AuthMetricEvent EmailVerification()
        {
            return new AuthMetricEvent(AuthMetricKind.EmailVerification, Value: null, RecordSessionCreate: false);
        }

        public void Record(AuthBusinessMetrics metrics, int statusCode)
        {
            switch (Kind)
            {
                case AuthMetricKind.Authentication:
                    var authenticationOutcome = ResolveAuthenticationOutcome(statusCode);
                    metrics.RecordAuthenticationAttempt(Value!, authenticationOutcome.Result, authenticationOutcome.Reason);
                    if (RecordSessionCreate && authenticationOutcome.Result == AuthBusinessMetrics.ResultSuccess)
                    {
                        metrics.RecordSessionOperation(
                            AuthBusinessMetrics.OperationCreate,
                            AuthBusinessMetrics.ResultSuccess,
                            AuthBusinessMetrics.ReasonNone);
                    }

                    break;

                case AuthMetricKind.RefreshToken:
                    var refreshTokenOutcome = ResolveRefreshTokenOutcome(statusCode);
                    metrics.RecordRefreshTokenOperation(Value!, refreshTokenOutcome.Result, refreshTokenOutcome.Reason);
                    break;

                case AuthMetricKind.Session:
                    var sessionOutcome = ResolveSessionOutcome(statusCode);
                    metrics.RecordSessionOperation(Value!, sessionOutcome.Result, sessionOutcome.Reason);
                    break;

                case AuthMetricKind.Registration:
                    var registrationOutcome = ResolveRegistrationOutcome(statusCode);
                    metrics.RecordRegistrationAttempt(registrationOutcome.Result, registrationOutcome.Reason);
                    break;

                case AuthMetricKind.EmailVerification:
                    var emailVerificationOutcome = ResolveEmailVerificationOutcome(statusCode);
                    metrics.RecordEmailVerificationAttempt(
                        emailVerificationOutcome.Result,
                        emailVerificationOutcome.Reason);
                    break;
            }
        }

        public void RecordCancelled(AuthBusinessMetrics metrics)
        {
            switch (Kind)
            {
                case AuthMetricKind.Authentication:
                    metrics.RecordAuthenticationAttempt(
                        Value!,
                        AuthBusinessMetrics.ResultCancelled,
                        AuthBusinessMetrics.ReasonCancelled);
                    break;

                case AuthMetricKind.RefreshToken:
                    metrics.RecordRefreshTokenOperation(
                        Value!,
                        AuthBusinessMetrics.ResultCancelled,
                        AuthBusinessMetrics.ReasonCancelled);
                    break;

                case AuthMetricKind.Session:
                    metrics.RecordSessionOperation(
                        Value!,
                        AuthBusinessMetrics.ResultCancelled,
                        AuthBusinessMetrics.ReasonCancelled);
                    break;

                case AuthMetricKind.Registration:
                    metrics.RecordRegistrationAttempt(
                        AuthBusinessMetrics.ResultCancelled,
                        AuthBusinessMetrics.ReasonCancelled);
                    break;

                case AuthMetricKind.EmailVerification:
                    metrics.RecordEmailVerificationAttempt(
                        AuthBusinessMetrics.ResultCancelled,
                        AuthBusinessMetrics.ReasonCancelled);
                    break;
            }
        }
    }

    private static AuthMetricOutcome ResolveAuthenticationOutcome(int statusCode)
    {
        return statusCode switch
        {
            >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices =>
                AuthMetricOutcome.Success(),
            StatusCodes.Status429TooManyRequests =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultBlocked, AuthBusinessMetrics.ReasonRateLimited),
            StatusCodes.Status401Unauthorized =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonInvalidCredentials),
            StatusCodes.Status403Forbidden =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonUserInactive),
            StatusCodes.Status400BadRequest =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonValidationError),
            _ => AuthMetricOutcome.UnknownFailure()
        };
    }

    private static AuthMetricOutcome ResolveRefreshTokenOutcome(int statusCode)
    {
        return statusCode switch
        {
            >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices =>
                AuthMetricOutcome.Success(),
            StatusCodes.Status400BadRequest =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonValidationError),
            StatusCodes.Status401Unauthorized =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonInvalidToken),
            StatusCodes.Status403Forbidden =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonUserInactive),
            _ => AuthMetricOutcome.UnknownFailure()
        };
    }

    private static AuthMetricOutcome ResolveSessionOutcome(int statusCode)
    {
        return statusCode switch
        {
            >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices =>
                AuthMetricOutcome.Success(),
            StatusCodes.Status404NotFound =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonSessionNotFound),
            StatusCodes.Status401Unauthorized =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonSessionNotFound),
            >= StatusCodes.Status500InternalServerError =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, "storage_failure"),
            _ => AuthMetricOutcome.UnknownFailure()
        };
    }

    private static AuthMetricOutcome ResolveRegistrationOutcome(int statusCode)
    {
        return statusCode switch
        {
            >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices =>
                AuthMetricOutcome.Success(),
            StatusCodes.Status409Conflict =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonDuplicateEmail),
            StatusCodes.Status400BadRequest =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonValidationError),
            >= StatusCodes.Status500InternalServerError =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonPersistenceFailure),
            _ => AuthMetricOutcome.UnknownFailure()
        };
    }

    private static AuthMetricOutcome ResolveEmailVerificationOutcome(int statusCode)
    {
        return statusCode switch
        {
            >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices =>
                AuthMetricOutcome.Success(),
            StatusCodes.Status400BadRequest =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonInvalidToken),
            StatusCodes.Status404NotFound =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, "user_not_found"),
            StatusCodes.Status409Conflict =>
                new AuthMetricOutcome(AuthBusinessMetrics.ResultNoop, "already_verified"),
            _ => AuthMetricOutcome.UnknownFailure()
        };
    }

    private enum AuthMetricKind
    {
        Authentication,
        RefreshToken,
        Session,
        Registration,
        EmailVerification
    }

    private sealed record AuthMetricOutcome(string Result, string Reason)
    {
        public static AuthMetricOutcome Success()
        {
            return new AuthMetricOutcome(AuthBusinessMetrics.ResultSuccess, AuthBusinessMetrics.ReasonNone);
        }

        public static AuthMetricOutcome UnknownFailure()
        {
            return new AuthMetricOutcome(AuthBusinessMetrics.ResultFailure, AuthBusinessMetrics.ReasonUnknown);
        }
    }
}
