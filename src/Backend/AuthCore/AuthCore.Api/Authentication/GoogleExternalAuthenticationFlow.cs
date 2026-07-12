using System.Diagnostics;
using AuthCore.Api.Observability;
using AuthCore.Api.Security;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
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
    private const string FailureResult = "failure";
    private const string GoogleCompletionPath = "/api/auth/external/google/complete";
    private const string MissingRequiredClaimsReason = "missing_required_claims";
    private const string SuccessResult = "success";

    private readonly IAuthenticationCookieWriter _authenticationCookieWriter;
    private readonly ICompleteGoogleLoginUseCase _completeGoogleLoginUseCase;
    private readonly IExternalReturnUrlValidator _externalReturnUrlValidator;
    private readonly IGoogleExternalLoginCommandFactory _googleExternalLoginCommandFactory;
    private readonly IGoogleOnboardingTicketStore _googleOnboardingTicketStore;
    private readonly ILogger<GoogleExternalAuthenticationFlow> _logger;
    private readonly ExternalAuthenticationMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="authenticationCookieWriter">Emissor dos cookies de autenticacao.</param>
    /// <param name="completeGoogleLoginUseCase">Caso de uso de conclusao do login Google.</param>
    /// <param name="externalReturnUrlValidator">Validador da URL de retorno.</param>
    /// <param name="googleExternalLoginCommandFactory">Fabrica do comando de login Google.</param>
    /// <param name="googleOnboardingTicketStore">Store do ticket temporario de onboarding Google.</param>
    /// <param name="metrics">Metricas do fluxo de autenticacao externa.</param>
    /// <param name="timeProvider">Provedor de tempo do fluxo.</param>
    /// <param name="logger">Logger do fluxo de autenticacao externa.</param>
    public GoogleExternalAuthenticationFlow(
        IAuthenticationCookieWriter authenticationCookieWriter,
        ICompleteGoogleLoginUseCase completeGoogleLoginUseCase,
        IExternalReturnUrlValidator externalReturnUrlValidator,
        IGoogleExternalLoginCommandFactory googleExternalLoginCommandFactory,
        IGoogleOnboardingTicketStore googleOnboardingTicketStore,
        ExternalAuthenticationMetrics metrics,
        TimeProvider timeProvider,
        ILogger<GoogleExternalAuthenticationFlow> logger)
    {
        ArgumentNullException.ThrowIfNull(authenticationCookieWriter);
        ArgumentNullException.ThrowIfNull(completeGoogleLoginUseCase);
        ArgumentNullException.ThrowIfNull(externalReturnUrlValidator);
        ArgumentNullException.ThrowIfNull(googleExternalLoginCommandFactory);
        ArgumentNullException.ThrowIfNull(googleOnboardingTicketStore);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _authenticationCookieWriter = authenticationCookieWriter;
        _completeGoogleLoginUseCase = completeGoogleLoginUseCase;
        _externalReturnUrlValidator = externalReturnUrlValidator;
        _googleExternalLoginCommandFactory = googleExternalLoginCommandFactory;
        _googleOnboardingTicketStore = googleOnboardingTicketStore;
        _metrics = metrics;
        _timeProvider = timeProvider;
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
        _metrics.RecordGoogleLoginStarted();
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

        var startedAtUtc = _timeProvider.GetUtcNow();
        var externalAuthentication = await httpContext.AuthenticateAsync(
            ExternalAuthenticationDefaults.ExternalScheme);

        cancellationToken.ThrowIfCancellationRequested();

        if (!externalAuthentication.Succeeded || externalAuthentication.Principal is null)
        {
            return await CompleteFailureAsync(
                httpContext,
                startedAtUtc,
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
                startedAtUtc,
                MissingRequiredClaimsReason);
        }

        await httpContext.SignOutAsync(ExternalAuthenticationDefaults.ExternalScheme);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _completeGoogleLoginUseCase.Execute(command, cancellationToken);

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

        _metrics.RecordGoogleLoginSucceeded(result.RequiresOnboarding);
        RecordDuration(startedAtUtc, SuccessResult);
        _logger.LogInformation(
            "GoogleLoginSucceeded. TraceId={TraceId}, UserIdentifier={UserIdentifier}, RequiresOnboarding={RequiresOnboarding}.",
            Activity.Current?.TraceId.ToString(),
            result.Session?.UserIdentifier,
            result.RequiresOnboarding);

        return new GoogleExternalAuthenticationResult(result.RedirectUrl);
    }

    private async Task<GoogleExternalAuthenticationResult> CompleteFailureAsync(
        HttpContext httpContext,
        DateTimeOffset startedAtUtc,
        string reason)
    {
        await httpContext.SignOutAsync(ExternalAuthenticationDefaults.ExternalScheme);

        if (reason.Equals(ExternalCallbackFailedReason, StringComparison.Ordinal))
            _metrics.RecordGoogleLoginCancelled();

        _metrics.RecordGoogleLoginFailed(reason);
        RecordDuration(startedAtUtc, reason.Equals(ExternalCallbackFailedReason, StringComparison.Ordinal)
            ? "cancelled"
            : FailureResult);
        _logger.LogWarning(
            "GoogleLoginFailed. TraceId={TraceId}, FailureReason={FailureReason}.",
            Activity.Current?.TraceId.ToString(),
            reason);

        return new GoogleExternalAuthenticationResult(ExternalCallbackFailedRedirectUrl);
    }

    private void RecordDuration(DateTimeOffset startedAtUtc, string result)
    {
        _metrics.RecordGoogleCallbackDuration(_timeProvider.GetUtcNow() - startedAtUtc, result);
    }
}
