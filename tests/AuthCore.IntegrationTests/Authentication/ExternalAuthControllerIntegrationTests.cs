using System.Net;
using System.Security.Claims;
using AuthCore.Api;
using AuthCore.Api.Authentication;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Controllers;
using AuthCore.Api.Observability;
using AuthCore.Api.Security;
using AuthCore.Application;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthCore.IntegrationTests.Authentication;

/// <summary>
/// Verifica a exposicao e comportamento HTTP do controller de autenticacao externa.
/// </summary>
public sealed class ExternalAuthControllerIntegrationTests
{
    [Fact]
    public void Google_WhenReturnUrlIsAllowed_ShouldReturnChallenge()
    {
        var validator = new StubExternalReturnUrlValidator
        {
            Result = "http://localhost:5173/dashboard"
        };
        var controller = CreateController(returnUrlValidator: validator);

        var result = controller.Google("http://localhost:5173/dashboard");

        var challengeResult = Assert.IsType<ChallengeResult>(result);

        Assert.Equal("http://localhost:5173/dashboard", validator.LastReturnUrl);
        Assert.Contains("Google", challengeResult.AuthenticationSchemes);
        Assert.Equal("/api/auth/external/google/complete", challengeResult.Properties!.RedirectUri);
        Assert.NotEqual("/api/auth/external/google/callback", challengeResult.Properties.RedirectUri);
        Assert.Equal("http://localhost:5173/dashboard", challengeResult.Properties.Items["returnUrl"]);
    }

    [Fact]
    public void Google_WhenReturnUrlIsInvalid_ShouldThrowValidationException()
    {
        var validator = new ThrowingExternalReturnUrlValidator();
        var controller = CreateController(returnUrlValidator: validator);

        var exception = Assert.Throws<ValidationException>(() =>
            controller.Google("https://evil.example/callback"));

        Assert.Equal("A URL de retorno informada nao e permitida.", exception.Message);
    }

    [Fact]
    public void Google_WhenEndpointIsDeclared_ShouldDocumentSwaggerResponses()
    {
        var method = typeof(ExternalAuthController).GetMethod(nameof(ExternalAuthController.Google))!;

        AssertProducesResponse(method, StatusCodes.Status302Found);
        AssertProducesResponse(method, StatusCodes.Status401Unauthorized);
        AssertProducesResponse(method, StatusCodes.Status400BadRequest, typeof(ResponseErrorJson));
    }

    [Fact]
    public async Task Google_WhenReturnUrlIsAllowed_ShouldChallengeGoogleOverHttp()
    {
        await using var app = BuildHttpApplication(googleConfigured: true);

        await StartExternalAuthPipelineAsync(app);

        using var httpClient = CreateHttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{GetAddress(app)}/api/auth/external/google?returnUrl=http%3A%2F%2Flocalhost%3A5173%2Fdashboard");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "api.authcore.dev");

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location;
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location!.Query);

        Assert.Equal("accounts.google.com", location.Host);
        Assert.Equal("https://api.authcore.dev/api/auth/external/google/callback", query["redirect_uri"].Single());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value =>
            value.Contains(".AspNetCore.Correlation.", StringComparison.Ordinal)
            && value.Contains("secure", StringComparison.OrdinalIgnoreCase)
            && value.Contains("samesite=none", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Google_WhenReturnUrlIsInvalid_ShouldRejectRequestOverHttp()
    {
        await using var app = BuildHttpApplication(googleConfigured: false);

        await StartExternalAuthPipelineAsync(app);

        using var httpClient = CreateHttpClient();
        using var response = await httpClient.GetAsync($"{GetAddress(app)}/api/auth/external/google?returnUrl=https%3A%2F%2Fevil.example%2Fcallback");
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("A URL de retorno informada", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Callback_WhenExternalAuthenticationFails_ShouldRedirectToError()
    {
        var authenticationService = new StubAuthenticationService(AuthenticateResult.Fail("invalid_state"));
        var useCase = new SpyCompleteGoogleLoginUseCase();
        var controller = CreateController(authenticationService, useCase: useCase);

        var result = await controller.GoogleComplete(CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectResult>(result);

        Assert.Equal("/auth/error?reason=external_callback_failed", redirectResult.Url);
        Assert.Equal("AuthCore.External", authenticationService.LastAuthenticateScheme);
        Assert.Equal("AuthCore.External", authenticationService.LastSignOutScheme);
        Assert.Null(useCase.LastCommand);
    }

    [Fact]
    public async Task Callback_WhenRequestIsCancelled_ShouldStopBeforeAuthentication()
    {
        var authenticationService = new StubAuthenticationService(AuthenticateResult.NoResult());
        var useCase = new SpyCompleteGoogleLoginUseCase();
        var controller = CreateController(authenticationService, useCase: useCase);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            controller.GoogleComplete(cancellationTokenSource.Token));

        Assert.Null(authenticationService.LastAuthenticateScheme);
        Assert.Null(useCase.LastCommand);
    }

    [Fact]
    public async Task Callback_WhenExternalPrincipalIsMissing_ShouldReturnSafeFailureOverHttp()
    {
        await using var app = BuildHttpApplication(googleConfigured: false);

        await StartExternalAuthPipelineAsync(app);

        using var httpClient = CreateHttpClient();
        using var response = await httpClient.GetAsync($"{GetAddress(app)}/api/auth/external/google/complete");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/error?reason=external_callback_failed", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Callback_WhenRequiredClaimsAreMissing_ShouldRedirectToError()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.Email, "bruno@authcore.dev"));
        var authenticationService = new StubAuthenticationService(CreateSuccessAuthentication(principal));
        var useCase = new SpyCompleteGoogleLoginUseCase();
        var controller = CreateController(authenticationService, useCase: useCase);

        var result = await controller.GoogleComplete(CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectResult>(result);

        Assert.Equal("/auth/error?reason=external_callback_failed", redirectResult.Url);
        Assert.Equal("AuthCore.External", authenticationService.LastSignOutScheme);
        Assert.Null(useCase.LastCommand);
    }

    [Fact]
    public async Task Callback_WhenExternalAuthenticationSucceeds_ShouldCallUseCase()
    {
        var authenticationProperties = new AuthenticationProperties();
        authenticationProperties.Items["returnUrl"] = "http://localhost:5173/dashboard";
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, "google-sub-123"),
            new Claim(ClaimTypes.Email, "bruno@authcore.dev"),
            new Claim("email_verified", "true"),
            new Claim(ClaimTypes.Name, "Bruno Silva"),
            new Claim("picture", "https://lh3.googleusercontent.com/avatar"));
        var authenticationService = new StubAuthenticationService(CreateSuccessAuthentication(
            principal,
            authenticationProperties));
        var useCase = new SpyCompleteGoogleLoginUseCase
        {
            Result = new CompleteGoogleLoginResult
            {
                RedirectUrl = "http://localhost:5173/dashboard"
            }
        };
        var controller = CreateController(authenticationService, useCase: useCase);
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await controller.GoogleComplete(cancellationTokenSource.Token);

        var redirectResult = Assert.IsType<RedirectResult>(result);

        Assert.Equal("http://localhost:5173/dashboard", redirectResult.Url);
        Assert.Equal("google-sub-123", useCase.LastCommand!.ProviderUserId);
        Assert.Equal("bruno@authcore.dev", useCase.LastCommand.Email);
        Assert.True(useCase.LastCommand.EmailVerified);
        Assert.Equal("Bruno Silva", useCase.LastCommand.FullName);
        Assert.Equal("https://lh3.googleusercontent.com/avatar", useCase.LastCommand.PictureUrl);
        Assert.Equal("http://localhost:5173/dashboard", useCase.LastCommand.ReturnUrl);
        Assert.Equal("127.0.0.1", useCase.LastCommand.IpAddress);
        Assert.Equal("AuthCore.IntegrationTests", useCase.LastCommand.UserAgent);
        Assert.Equal(cancellationTokenSource.Token, useCase.LastCancellationToken);
        Assert.Equal("AuthCore.External", authenticationService.LastSignOutScheme);
    }

    [Fact]
    public void Callback_WhenEndpointIsDeclared_ShouldDocumentSwaggerResponses()
    {
        var method = typeof(ExternalAuthController).GetMethod(nameof(ExternalAuthController.GoogleComplete))!;

        AssertProducesResponse(method, StatusCodes.Status302Found);
        AssertProducesResponse(method, StatusCodes.Status400BadRequest, typeof(ResponseErrorJson));
        AssertProducesResponse(method, StatusCodes.Status401Unauthorized, typeof(ResponseErrorJson));
        AssertProducesResponse(method, StatusCodes.Status403Forbidden, typeof(ResponseErrorJson));
        AssertProducesResponse(method, StatusCodes.Status404NotFound, typeof(ResponseErrorJson));
        AssertProducesResponse(method, StatusCodes.Status409Conflict, typeof(ResponseErrorJson));
    }

    [Fact]
    public async Task Callback_WhenLoginSucceeds_ShouldAppendAuthenticationCookies()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "google-sub-123"),
            new Claim("email", "bruno@authcore.dev"),
            new Claim("urn:google:email_verified", "true"));
        var authenticationService = new StubAuthenticationService(CreateSuccessAuthentication(principal));
        var useCase = new SpyCompleteGoogleLoginUseCase
        {
            Result = new CompleteGoogleLoginResult
            {
                RedirectUrl = "http://localhost:5173/dashboard",
                Session = new AuthenticatedUserSessionResult
                {
                    SessionId = "session-123",
                    AccessToken = "access-token-123",
                    AccessTokenExpiresAtUtc = new DateTime(2026, 6, 15, 18, 10, 0, DateTimeKind.Utc),
                    ExpiresAtUtc = new DateTime(2026, 6, 15, 19, 0, 0, DateTimeKind.Utc),
                    UserIdentifier = Guid.NewGuid(),
                    Email = "bruno@authcore.dev"
                }
            }
        };
        var controller = CreateController(authenticationService, useCase: useCase);

        var result = await controller.GoogleComplete(CancellationToken.None);
        var setCookieHeader = controller.Response.Headers.SetCookie.ToString();

        var redirectResult = Assert.IsType<RedirectResult>(result);

        Assert.Equal("http://localhost:5173/dashboard", redirectResult.Url);
        Assert.Contains("sid=session-123", setCookieHeader, StringComparison.Ordinal);
        Assert.Contains("at=access-token-123", setCookieHeader, StringComparison.Ordinal);
        Assert.Contains("XSRF-TOKEN=csrf-token-session-123", setCookieHeader, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Callback_WhenLoginSucceeds_ShouldWriteSanitizedLog()
    {
        var logger = new SpyLogger<GoogleExternalAuthenticationFlow>();
        var principal = CreatePrincipal(
            new Claim("sub", "google-sub-123"),
            new Claim("email", "bruno@authcore.dev"),
            new Claim("urn:google:email_verified", "true"));
        var authenticationService = new StubAuthenticationService(CreateSuccessAuthentication(principal));
        var useCase = new SpyCompleteGoogleLoginUseCase
        {
            Result = new CompleteGoogleLoginResult
            {
                RedirectUrl = "http://localhost:5173/dashboard",
                Session = new AuthenticatedUserSessionResult
                {
                    SessionId = "session-123",
                    AccessToken = "access-token-123",
                    AccessTokenExpiresAtUtc = new DateTime(2026, 6, 15, 18, 10, 0, DateTimeKind.Utc),
                    ExpiresAtUtc = new DateTime(2026, 6, 15, 19, 0, 0, DateTimeKind.Utc),
                    UserIdentifier = Guid.Parse("2cd11184-b3a8-4d16-8db7-dd3f39f377ea"),
                    Email = "bruno@authcore.dev"
                }
            }
        };
        var controller = CreateController(
            authenticationService,
            logger,
            useCase: useCase);

        await controller.GoogleComplete(CancellationToken.None);

        var logMessage = Assert.Single(logger.Messages, message => message.Contains("GoogleLoginSucceeded", StringComparison.Ordinal));

        Assert.Contains("2cd11184-b3a8-4d16-8db7-dd3f39f377ea", logMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("bruno@authcore.dev", logMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session-123", logMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access-token-123", logMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("XSRF-TOKEN", logMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DevelopmentSettings_WhenVersioned_ShouldNotContainSecrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GetDevelopmentAppsettingsPath(), optional: false)
            .Build();
        var allowedReturnUrls = configuration
            .GetSection("Authentication:AllowedReturnUrls")
            .Get<string[]>()
            ?? [];

        Assert.True(string.IsNullOrWhiteSpace(configuration["ConnectionStrings:PostgreSql"]));
        Assert.True(string.IsNullOrWhiteSpace(configuration["Authentication:Jwt:SigningKey"]));
        Assert.True(string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]));
        Assert.True(string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]));
        Assert.True(string.IsNullOrWhiteSpace(configuration["Redis:ConnectionString"]));
        Assert.True(string.IsNullOrWhiteSpace(configuration["Auth:Csrf:SigningKey"]));
        Assert.True(string.IsNullOrWhiteSpace(configuration["DataProtection:CertificatePassword"]));
        Assert.True(string.IsNullOrWhiteSpace(configuration["RabbitMq:Username"]));
        Assert.True(string.IsNullOrWhiteSpace(configuration["RabbitMq:Password"]));
        Assert.Equal(
            "/api/auth/external/google/callback",
            configuration["Authentication:Google:CallbackPath"]);
        Assert.Contains("http://localhost:5173", allowedReturnUrls);
        Assert.Contains("http://localhost:3000", allowedReturnUrls);
        Assert.DoesNotContain(allowedReturnUrls, string.IsNullOrWhiteSpace);
    }

    private static ExternalAuthController CreateController(
        IAuthenticationService? authenticationService = null,
        ILogger<GoogleExternalAuthenticationFlow>? logger = null,
        IExternalReturnUrlValidator? returnUrlValidator = null,
        ICompleteGoogleLoginUseCase? useCase = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "AuthCore.IntegrationTests";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(authenticationService ?? new StubAuthenticationService(AuthenticateResult.NoResult()))
            .BuildServiceProvider();

        var authenticationCookieWriter = new AuthenticationCookieWriter(
                new StubCsrfTokenService(),
                CreateAuthCookieOptions(),
                Options.Create(new CsrfOptions
                {
                    CookieName = "XSRF-TOKEN",
                    HeaderName = "X-CSRF-TOKEN",
                    SigningKey = "tests-csrf-signing-key-2026"
                }));
        var googleOnboardingTicketStore = new SpyGoogleOnboardingTicketStore();
        var flow = new GoogleExternalAuthenticationFlow(
            authenticationCookieWriter,
            useCase ?? new SpyCompleteGoogleLoginUseCase(),
            returnUrlValidator ?? new StubExternalReturnUrlValidator(),
            new GoogleExternalLoginCommandFactory(),
            googleOnboardingTicketStore,
            new AuthBusinessMetrics(),
            logger ?? new SpyLogger<GoogleExternalAuthenticationFlow>());

        return new ExternalAuthController(
            flow,
            googleOnboardingTicketStore,
            authenticationCookieWriter,
            new StubTrustedOriginValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static WebApplication BuildHttpApplication(bool googleConfigured)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        builder.Configuration.AddInMemoryCollection(CreateHttpConfiguration(googleConfigured));
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(ExternalAuthController).Assembly);
        builder.Services.AddApi(builder.Configuration);
        builder.Services.AddSingleton<IDataProtectionProvider, EphemeralDataProtectionProvider>();
        builder.Services.AddApplication();
        builder.Services.AddSingleton(Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            AccessTokenCookieName = "at",
            Secure = false
        }));
        builder.Services.AddSingleton(Options.Create(new CsrfOptions
        {
            CookieName = "XSRF-TOKEN",
            HeaderName = "X-CSRF-TOKEN",
            SigningKey = "tests-csrf-signing-key-2026",
            AllowedOrigins = ["http://localhost:5173"]
        }));
        builder.Services.AddScoped<ICompleteGoogleLoginUseCase, SpyCompleteGoogleLoginUseCase>();

        return builder.Build();
    }

    private static async Task StartExternalAuthPipelineAsync(WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseCors("AuthCoreBrowserSession");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.Urls.Add("http://127.0.0.1:0");

        await app.StartAsync();
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
    }

    private static string GetAddress(WebApplication app)
    {
        return app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();
    }

    private static Dictionary<string, string?> CreateHttpConfiguration(bool googleConfigured)
    {
        return new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = "AuthCore-Tests-SigningKey-2026-Strong!",
            ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = "5",
            ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Authentication:Google:ClientId"] = googleConfigured ? "google-client-id" : string.Empty,
            ["Authentication:Google:ClientSecret"] = googleConfigured ? "google-client-secret" : string.Empty,
            ["Authentication:Google:CallbackPath"] = "/api/auth/external/google/callback",
            ["Authentication:DefaultReturnUrl"] = "http://localhost:5173",
            ["Authentication:AllowedReturnUrls:0"] = "http://localhost:5173",
            ["Auth:Cookie:SessionCookieName"] = "sid",
            ["Auth:Cookie:AccessTokenCookieName"] = "at",
            ["Auth:Cookie:Secure"] = "false",
            ["Auth:Csrf:SigningKey"] = "tests-csrf-signing-key-2026",
            ["Auth:Csrf:AllowedOrigins:0"] = "http://localhost:5173",
            ["ReverseProxy:KnownProxies:0"] = "127.0.0.1"
        };
    }

    private static IOptions<AuthCookieOptions> CreateAuthCookieOptions()
    {
        return Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            AccessTokenCookieName = "at",
            HttpOnly = true,
            Path = "/",
            SameSite = "Strict",
            Secure = false
        });
    }

    private static AuthenticateResult CreateSuccessAuthentication(
        ClaimsPrincipal principal,
        AuthenticationProperties? authenticationProperties = null)
    {
        return AuthenticateResult.Success(new AuthenticationTicket(
            principal,
            authenticationProperties ?? new AuthenticationProperties(),
            "AuthCore.External"));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Google"));
    }

    private static void AssertProducesResponse(
        System.Reflection.MethodInfo method,
        int statusCode,
        Type? responseType = null)
    {
        var attributes = method
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: false)
            .Cast<ProducesResponseTypeAttribute>();

        Assert.Contains(attributes, attribute =>
            attribute.StatusCode == statusCode &&
            (responseType is null || attribute.Type == responseType));
    }

    private static string GetDevelopmentAppsettingsPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuthCore.sln")))
            {
                return Path.Combine(
                    directory.FullName,
                    "src",
                    "Backend",
                    "AuthCore",
                    "AuthCore.Api",
                    "appsettings.Development.json");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Diretorio raiz do repositorio nao foi encontrado.");
    }

    private sealed class StubExternalReturnUrlValidator : IExternalReturnUrlValidator
    {
        public string Result { get; init; } = "/";

        public string? LastReturnUrl { get; private set; }

        public string Validate(string? returnUrl)
        {
            LastReturnUrl = returnUrl;
            return Result;
        }
    }

    private sealed class ThrowingExternalReturnUrlValidator : IExternalReturnUrlValidator
    {
        public string Validate(string? returnUrl)
        {
            throw new ValidationException("A URL de retorno informada nao e permitida.");
        }
    }

    private sealed class SpyCompleteGoogleLoginUseCase : ICompleteGoogleLoginUseCase
    {
        public CompleteGoogleLoginCommand? LastCommand { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public CompleteGoogleLoginResult Result { get; init; } = new()
        {
            RedirectUrl = "http://localhost:5173"
        };

        public Task<CompleteGoogleLoginResult> Execute(
            CompleteGoogleLoginCommand command,
            CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Result);
        }
    }

    private sealed class SpyGoogleOnboardingTicketStore : IGoogleOnboardingTicketStore
    {
        public CompleteGoogleOnboardingTicketCommand? LastCommand { get; private set; }

        public bool Deleted { get; private set; }

        public GoogleOnboardingTicket? Ticket { get; init; }

        public void Append(HttpResponse response, CompleteGoogleOnboardingTicketCommand command)
        {
            LastCommand = command;
        }

        public GoogleOnboardingTicket? Read(HttpRequest request)
        {
            return Ticket;
        }

        public void Delete(HttpResponse response)
        {
            Deleted = true;
        }
    }

    private sealed class StubCsrfTokenService : ICsrfTokenService
    {
        public string Generate(string sessionId)
        {
            return $"csrf-token-{sessionId}";
        }

        public bool IsValid(string sessionId, string token)
        {
            return true;
        }
    }

    private sealed class StubTrustedOriginValidator : ITrustedOriginValidator
    {
        public void Validate(HttpRequest request)
        {
        }
    }

    private sealed class StubAuthenticationService : IAuthenticationService
    {
        private readonly AuthenticateResult _authenticateResult;

        public StubAuthenticationService(AuthenticateResult authenticateResult)
        {
            _authenticateResult = authenticateResult;
        }

        public string? LastAuthenticateScheme { get; private set; }

        public string? LastSignOutScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            LastAuthenticateScheme = scheme;
            return Task.FromResult(_authenticateResult);
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            LastSignOutScheme = scheme;
            return Task.CompletedTask;
        }
    }

    private sealed class SpyLogger<T> : ILogger<T>
    {
        public IList<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
