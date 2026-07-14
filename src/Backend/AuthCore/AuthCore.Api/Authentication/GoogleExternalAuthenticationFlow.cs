using System.Diagnostics;
using AuthCore.Api.Observability;
using AuthCore.Api.Security;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using AuthCore.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Authentication;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa fluxo de autenticacao externa com Google.
/// </summary>
internal sealed class GoogleExternalAuthenticationFlow : IGoogleExternalAuthenticationFlow
{
    private const string ExternalCallbackFailedRedirectUrl =
        "/auth/error?reason=external_callback_failed";
    private const string ExternalCallbackFailedReason = "external_callback_failed";
    private const string GoogleCompletionPath = "/api/auth/external/google/complete";
    private const string MissingRequiredClaimsReason = "missing_required_claims";

    private readonly IAuthenticationCookieWriter _authenticationCookieWriter;
    private readonly ICompleteGoogleLoginUseCase _completeGoogleLoginUseCase;
    private readonly IExternalReturnUrlValidator _externalReturnUrlValidator;
    private readonly IGoogleExternalLoginCommandFactory _googleExternalLoginCommandFactory;
    private readonly IGoogleOnboardingTicketStore _googleOnboardingTicketStore;
    private readonly ILogger<GoogleExternalAuthenticationFlow> _logger;
    private readonly AuthBusinessMetrics _metrics;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="authenticationCookieWriter">Emissor dos cookies de autenticacao.</param>
    /// <param name="completeGoogleLoginUseCase">Caso de uso de conclusao do login Google.</param>
    /// <param name="externalReturnUrlValidator">Validador da URL de retorno.</param>
    /// <param name="googleExternalLoginCommandFactory">Fabrica do comando de login Google.</param>
    /// <param name="googleOnboardingTicketStore">Store do ticket temporario de onboarding Google.</param>
    /// <param name="metrics">Métricas dos fluxos de autenticação.</param>
    /// <param name="logger">Logger do fluxo de autenticacao externa.</param>
    public GoogleExternalAuthenticationFlow(
        IAuthenticationCookieWriter authenticationCookieWriter,
        ICompleteGoogleLoginUseCase completeGoogleLoginUseCase,
        IExternalReturnUrlValidator externalReturnUrlValidator,
        IGoogleExternalLoginCommandFactory googleExternalLoginCommandFactory,
        IGoogleOnboardingTicketStore googleOnboardingTicketStore,
        AuthBusinessMetrics metrics,
        ILogger<GoogleExternalAuthenticationFlow> logger)
    {
        ArgumentNullException.ThrowIfNull(authenticationCookieWriter);
        ArgumentNullException.ThrowIfNull(completeGoogleLoginUseCase);
        ArgumentNullException.ThrowIfNull(externalReturnUrlValidator);
        ArgumentNullException.ThrowIfNull(googleExternalLoginCommandFactory);
        ArgumentNullException.ThrowIfNull(googleOnboardingTicketStore);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _authenticationCookieWriter = authenticationCookieWriter;
        _completeGoogleLoginUseCase = completeGoogleLoginUseCase;
        _externalReturnUrlValidator = externalReturnUrlValidator;
        _googleExternalLoginCommandFactory = googleExternalLoginCommandFactory;
        _googleOnboardingTicketStore = googleOnboardingTicketStore;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc />
    public AuthenticationProperties CreateChallengeProperties(
        HttpContext httpContext,
        string? returnUrl)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        _googleOnboardingTicketStore.Delete(httpContext.Response);

        var safeReturnUrl = _externalReturnUrlValidator.Validate(returnUrl);
        _logger.LogInformation(
            "GoogleLoginStarted. TraceId={TraceId}, HasReturnUrl={HasReturnUrl}.",
            Activity.Current?.TraceId.ToString(),
            !string.IsNullOrWhiteSpace(returnUrl));

        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = GoogleCompletionPath
        };
        authenticationProperties.Items[ExternalAuthenticationDefaults.ReturnUrlPropertyName] =
            safeReturnUrl;

        return authenticationProperties;
    }

    /// <inheritdoc />
    public async Task<GoogleExternalAuthenticationResult> CompleteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        cancellationToken.ThrowIfCancellationRequested();

        var externalAuthentication = await httpContext.AuthenticateAsync(
            ExternalAuthenticationDefaults.ExternalScheme);

        cancellationToken.ThrowIfCancellationRequested();

        if (!externalAuthentication.Succeeded || externalAuthentication.Principal is null)
        {
            return await CompleteFailureAsync(
                httpContext,
                ExternalCallbackFailedReason);
        }

        var command = _googleExternalLoginCommandFactory.Create(
            externalAuthentication.Principal,
            externalAuthentication.Properties,
            new ExternalLoginRequestMetadata(
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString()));

        if (string.IsNullOrWhiteSpace(command.ProviderUserId)
            || string.IsNullOrWhiteSpace(command.Email))
        {
            return await CompleteFailureAsync(
                httpContext,
                MissingRequiredClaimsReason);
        }

        await httpContext.SignOutAsync(ExternalAuthenticationDefaults.ExternalScheme);
        cancellationToken.ThrowIfCancellationRequested();

        CompleteGoogleLoginResult result;
        try
        {
            result = await _completeGoogleLoginUseCase.Execute(command, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RecordGoogleAttempt(AuthBusinessMetrics.ResultCancelled, AuthBusinessMetrics.ReasonCancelled);
            throw;
        }
        catch (AuthCoreException exception)
        {
            RecordGoogleAttempt(AuthBusinessMetrics.ResultFailure, NormalizeProjectExceptionReason(exception));
            throw;
        }

        if (result.Session is not null)
        {
            _authenticationCookieWriter.AppendAuthenticatedSession(httpContext.Response, result.Session);
            _googleOnboardingTicketStore.Delete(httpContext.Response);
        }
        else if (result.RequiresOnboarding)
        {
            _googleOnboardingTicketStore.Append(
                httpContext.Response,
                new CompleteGoogleOnboardingTicketCommand(
                    command.ProviderUserId,
                    command.Email,
                    command.EmailVerified,
                    command.FullName,
                    command.PictureUrl));
        }

        if (result.Session is not null)
            RecordGoogleAttempt(AuthBusinessMetrics.ResultSuccess, AuthBusinessMetrics.ReasonNone);

        _logger.LogInformation(
            "GoogleLoginSucceeded. TraceId={TraceId}, UserIdentifier={UserIdentifier}, RequiresOnboarding={RequiresOnboarding}.",
            Activity.Current?.TraceId.ToString(),
            result.Session?.UserIdentifier,
            result.RequiresOnboarding);

        return new GoogleExternalAuthenticationResult(result.RedirectUrl);
    }

    private async Task<GoogleExternalAuthenticationResult> CompleteFailureAsync(
        HttpContext httpContext,
        string reason)
    {
        await httpContext.SignOutAsync(ExternalAuthenticationDefaults.ExternalScheme);

        RecordGoogleAttempt(
            reason.Equals(ExternalCallbackFailedReason, StringComparison.Ordinal)
                ? AuthBusinessMetrics.ResultCancelled
                : AuthBusinessMetrics.ResultFailure,
            NormalizeGoogleFailureReason(reason));
        _logger.LogWarning(
            "GoogleLoginFailed. TraceId={TraceId}, FailureReason={FailureReason}.",
            Activity.Current?.TraceId.ToString(),
            reason);

        return new GoogleExternalAuthenticationResult(ExternalCallbackFailedRedirectUrl);
    }

    private void RecordGoogleAttempt(string result, string reason)
    {
        _metrics.RecordAuthenticationAttempt(AuthBusinessMetrics.FlowGoogleSession, result, reason);
    }

    private static string NormalizeGoogleFailureReason(string reason)
    {
        return reason switch
        {
            ExternalCallbackFailedReason => AuthBusinessMetrics.ReasonCancelled,
            MissingRequiredClaimsReason => AuthBusinessMetrics.ReasonValidationError,
            _ => AuthBusinessMetrics.ReasonUnknown
        };
    }

    private static string NormalizeProjectExceptionReason(AuthCoreException exception)
    {
        return exception switch
        {
            ValidationException => AuthBusinessMetrics.ReasonValidationError,
            ForbiddenException => AuthBusinessMetrics.ReasonUserInactive,
            UnauthorizedException => AuthBusinessMetrics.ReasonInvalidCredentials,
            ConflictException => AuthBusinessMetrics.ReasonValidationError,
            NotFoundException => AuthBusinessMetrics.ReasonUnknown,
            _ => AuthBusinessMetrics.ReasonUnknown
        };
    }
}
