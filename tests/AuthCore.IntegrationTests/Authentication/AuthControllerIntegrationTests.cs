using System.Security.Claims;
using AuthCore.Api;
using AuthCore.Api.Authentication;
using AuthCore.Api.Contracts.Requests;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Controllers;
using AuthCore.Api.Security;
using AuthCore.Application.Authentication.Models;
using AuthCore.Application.Authentication.UseCases.GetUserSessions;
using AuthCore.Application.Authentication.UseCases.Login;
using AuthCore.Application.Authentication.UseCases.LoginSession;
using AuthCore.Application.Authentication.UseCases.LogoutAllSessions;
using AuthCore.Application.Authentication.UseCases.LogoutCurrentSession;
using AuthCore.Application.Authentication.UseCases.LogoutSession;
using AuthCore.Application.Authentication.UseCases.RefreshSession;
using AuthCore.Application.Authentication.UseCases.ResendVerification;
using AuthCore.Application.Authentication.UseCases.RevokeUserSession;
using AuthCore.Application.Authentication.UseCases.VerifyEmail;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.Users.UseCases.RegisterUser;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuthCore.IntegrationTests.Authentication;

/// <summary>
/// Verifica a exposição e o comportamento HTTP do controller de autenticação.
/// </summary>
public sealed class AuthControllerIntegrationTests
{
    [Fact]
    public async Task Register_WhenUseCaseSucceeds_ShouldReturnCreatedWithRegisteredUserResponse()
    {
        var userIdentifier = Guid.NewGuid();
        var useCase = new SpyRegisterUserUseCase
        {
            Result = new RegisterUserResult
            {
                UserIdentifier = userIdentifier,
                FullName = "Bruno Silva",
                Email = "bruno@authcore.dev"
            }
        };
        var controller = CreateAuthController();

        var result = await controller.Register(useCase, new RequestRegisterUserJson
        {
            FirstName = "Bruno",
            LastName = "Silva",
            Email = "bruno@authcore.dev",
            Contact = "11999999999",
            Password = "ValidPassword#2026",
            ConfirmPassword = "ValidPassword#2026"
        });

        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        var response = Assert.IsType<ResponseRegisteredUserJson>(createdResult.Value);

        Assert.Equal("Bruno", useCase.LastCommand!.FirstName);
        Assert.Equal("bruno@authcore.dev", useCase.LastCommand.Email);
        Assert.Equal(userIdentifier, response.UserIdentifier);
        Assert.Equal("Bruno Silva", response.FullName);
        Assert.Equal("bruno@authcore.dev", response.Email);
    }

    [Fact]
    public async Task VerifyEmail_WhenUseCaseSucceeds_ShouldReturnNoContentAndForwardCommand()
    {
        var useCase = new SpyVerifyEmailUseCase();
        var controller = CreateAuthController();

        var result = await controller.VerifyEmail(useCase, new RequestVerifyEmailJson
        {
            Email = "bruno@authcore.dev",
            Code = "123456"
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("bruno@authcore.dev", useCase.LastCommand!.Email);
        Assert.Equal("123456", useCase.LastCommand.Code);
    }

    [Fact]
    public async Task VerifyEmail_WhenUseCaseThrowsNotFoundException_ShouldReturnBadRequestWithGenericResponse()
    {
        var useCase = new ThrowingVerifyEmailUseCase(new NotFoundException("Nenhuma verificação pendente foi encontrada para o e-mail informado."));
        var controller = CreateAuthController();

        var result = await controller.VerifyEmail(useCase, new RequestVerifyEmailJson
        {
            Email = "missing@authcore.dev",
            Code = "123456"
        });

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ResponseErrorJson>(badRequestResult.Value);

        Assert.Equal(["Não foi possível validar o código de verificação informado."], response.Errors);
    }

    [Fact]
    public async Task ResendVerification_WhenUseCaseThrowsForbiddenException_ShouldReturnNoContent()
    {
        var useCase = new ThrowingResendVerificationUseCase(new ForbiddenException("O reenvio da verificação ainda está em cooldown."));
        var controller = CreateAuthController();

        var result = await controller.ResendVerification(useCase, new RequestResendVerificationJson
        {
            Email = "pending@authcore.dev"
        });

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Login_WhenSessionUseCaseSucceeds_ShouldReturnOkWithCookieAndAuthenticatedUserResponse()
    {
        var userId = Guid.NewGuid();
        var useCase = new SpyLoginSessionUseCase
        {
            Result = new AuthenticatedUserSessionResult
            {
                SessionId = "session-123",
                UserIdentifier = userId,
                Email = "bruno@authcore.dev",
                ExpiresAtUtc = new DateTime(2026, 4, 20, 15, 0, 0, DateTimeKind.Utc)
            }
        };
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController();

        var result = await controller.Login(useCase, CreateServiceProvider(authCookieOptions), new RequestSessionLoginJson
        {
            Email = "bruno@authcore.dev",
            Password = "ValidPassword#2026"
        });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ResponseAuthenticatedUserJson>(okResult.Value);
        var setCookieHeader = controller.Response.Headers.SetCookie.ToString();

        Assert.Equal("bruno@authcore.dev", useCase.LastCommand!.Email);
        Assert.Equal("ValidPassword#2026", useCase.LastCommand.Password);
        Assert.Equal(userId, response.UserId);
        Assert.Equal(useCase.Result.Email, response.Email);
        Assert.Contains("sid=session-123", setCookieHeader, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WhenUseCaseThrowsForbiddenException_ShouldReturnForbiddenResponseErrorJson()
    {
        var useCase = new ThrowingLoginSessionUseCase(new ForbiddenException("O usuário precisa verificar o e-mail antes de autenticar."));
        var controller = CreateSessionController();

        var result = await controller.Login(useCase, CreateServiceProvider(Options.Create(new AuthCookieOptions())), new RequestSessionLoginJson
        {
            Email = "pending@authcore.dev",
            Password = "ValidPassword#2026"
        });

        var forbiddenResult = Assert.IsType<ObjectResult>(result.Result);
        var response = Assert.IsType<ResponseErrorJson>(forbiddenResult.Value);

        Assert.Equal(StatusCodes.Status403Forbidden, forbiddenResult.StatusCode);
        Assert.Equal(["O usuário precisa verificar o e-mail antes de autenticar."], response.Errors);
    }

    [Fact]
    public async Task Token_WhenUseCaseSucceeds_ShouldReturnOkWithAuthenticatedSessionResponse()
    {
        var useCase = new SpyLoginUseCase
        {
            Result = new AuthenticatedSessionResult
            {
                AccessToken = "access-token",
                AccessTokenExpiresAtUtc = new DateTime(2026, 4, 13, 15, 0, 0, DateTimeKind.Utc),
                RefreshToken = "refresh-token",
                RefreshTokenExpiresAtUtc = new DateTime(2026, 4, 20, 15, 0, 0, DateTimeKind.Utc)
            }
        };
        var controller = CreateTokenController();

        var result = await controller.Login(useCase, new RequestLoginJson
        {
            Email = "bruno@authcore.dev",
            Password = "ValidPassword#2026"
        });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ResponseAuthenticatedSessionJson>(okResult.Value);

        Assert.Equal("bruno@authcore.dev", useCase.LastCommand!.Email);
        Assert.Equal("ValidPassword#2026", useCase.LastCommand.Password);
        Assert.Equal(useCase.Result.AccessToken, response.AccessToken);
        Assert.Equal(useCase.Result.RefreshToken, response.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WhenUseCaseSucceeds_ShouldReturnOkWithAuthenticatedSessionResponse()
    {
        var useCase = new SpyRefreshSessionUseCase
        {
            Result = new AuthenticatedSessionResult
            {
                AccessToken = "next-access-token",
                AccessTokenExpiresAtUtc = new DateTime(2026, 4, 13, 15, 15, 0, DateTimeKind.Utc),
                RefreshToken = "next-refresh-token",
                RefreshTokenExpiresAtUtc = new DateTime(2026, 4, 20, 15, 15, 0, DateTimeKind.Utc)
            }
        };
        var controller = CreateTokenController();

        var result = await controller.Refresh(useCase, new RequestRefreshSessionJson
        {
            RefreshToken = "current-refresh-token"
        });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ResponseAuthenticatedSessionJson>(okResult.Value);

        Assert.Equal("current-refresh-token", useCase.LastCommand!.RefreshToken);
        Assert.Equal(useCase.Result.AccessToken, response.AccessToken);
        Assert.Equal(useCase.Result.RefreshToken, response.RefreshToken);
    }

    [Fact]
    public async Task TokenLogout_WhenUseCaseSucceeds_ShouldReturnNoContentAndForwardRefreshToken()
    {
        var useCase = new SpyLogoutSessionUseCase();
        var controller = CreateTokenController();

        var result = await controller.Logout(useCase, new RequestTokenLogoutJson
        {
            RefreshToken = "refresh-token"
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("refresh-token", useCase.LastCommand!.RefreshToken);
    }

    [Fact]
    public async Task TokenLogout_WhenUseCaseThrowsUnauthorizedAccessException_ShouldReturnUnauthorizedResponseErrorJson()
    {
        var useCase = new ThrowingLogoutSessionUseCase(new UnauthorizedAccessException("A sessão informada é inválida ou expirou."));
        var controller = CreateTokenController();

        var result = await controller.Logout(useCase, new RequestTokenLogoutJson
        {
            RefreshToken = "invalid-refresh-token"
        });

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ResponseErrorJson>(unauthorizedResult.Value);

        Assert.Equal(["A sessão informada é inválida ou expirou."], response.Errors);
    }

    [Fact]
    public async Task TokenLogout_WhenUseCaseCompletesForRepeatedLogout_ShouldRemainNoContent()
    {
        var useCase = new SpyLogoutSessionUseCase();
        var controller = CreateTokenController();

        _ = await controller.Logout(useCase, new RequestTokenLogoutJson
        {
            RefreshToken = "refresh-token"
        });

        var result = await controller.Logout(useCase, new RequestTokenLogoutJson
        {
            RefreshToken = "refresh-token"
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("refresh-token", useCase.LastCommand!.RefreshToken);
    }

    [Fact]
    public void Me_WhenUserClaimsArePresent_ShouldReturnOkWithAuthenticatedUserResponse()
    {
        var userId = Guid.NewGuid();
        var controller = CreateSessionController(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "bruno@authcore.dev"),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-123"),
            new Claim(SessionAuthenticationDefaults.UserStatusClaimType, "Active"),
            new Claim(SessionAuthenticationDefaults.UserIsActiveClaimType, bool.TrueString)
        });

        var result = controller.Me();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ResponseAuthenticatedUserJson>(okResult.Value);

        Assert.Equal(userId, response.UserId);
        Assert.Equal("bruno@authcore.dev", response.Email);
    }

    [Fact]
    public void Me_WhenSessionUserIsPending_ShouldReturnForbiddenResponseErrorJson()
    {
        var controller = CreateSessionController(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "pending@authcore.dev"),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-123"),
            new Claim(SessionAuthenticationDefaults.UserStatusClaimType, "PendingEmailVerification"),
            new Claim(SessionAuthenticationDefaults.UserIsActiveClaimType, bool.TrueString)
        });

        var result = controller.Me();

        var forbiddenResult = Assert.IsType<ObjectResult>(result.Result);
        var response = Assert.IsType<ResponseErrorJson>(forbiddenResult.Value);

        Assert.Equal(StatusCodes.Status403Forbidden, forbiddenResult.StatusCode);
        Assert.Equal(["O usuário precisa verificar o e-mail antes de autenticar."], response.Errors);
    }

    [Fact]
    public async Task Logout_WhenUseCaseSucceeds_ShouldReturnNoContentAndDeleteCookie()
    {
        var useCase = new SpyLogoutCurrentSessionUseCase();
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController(new[]
        {
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-123")
        });

        var result = await controller.Logout(useCase, CreateServiceProvider(authCookieOptions));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("session-123", useCase.LastCommand!.SessionId);
        Assert.Contains("sid=", controller.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_WhenOriginIsNotAllowed_ShouldReturnForbiddenResponseErrorJson()
    {
        var useCase = new SpyLogoutCurrentSessionUseCase();
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController(
            [
                new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-123"),
                new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, Guid.NewGuid().ToString())
            ],
            csrfRequestValidator: CreateCsrfRequestValidator("https://app.authcore.dev"));

        controller.Request.Headers.Origin = "https://evil.authcore.dev";

        var result = await controller.Logout(useCase, CreateServiceProvider(authCookieOptions));

        var forbiddenResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<ResponseErrorJson>(forbiddenResult.Value);

        Assert.Equal(StatusCodes.Status403Forbidden, forbiddenResult.StatusCode);
        Assert.Equal(["A origem da requisição não é permitida."], response.Errors);
        Assert.Null(useCase.LastCommand);
    }

    [Fact]
    public async Task GetSessions_WhenUseCaseSucceeds_ShouldReturnOkWithSessionsResponse()
    {
        var userId = Guid.NewGuid();
        var useCase = new SpyGetUserSessionsUseCase
        {
            Result = new UserSessionsResult
            {
                CurrentSessionId = "session-123",
                Sessions =
                [
                    new UserSessionResult
                    {
                        SessionId = "session-123",
                        CreatedAtUtc = new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc),
                        LastSeenAtUtc = new DateTime(2026, 4, 18, 10, 30, 0, DateTimeKind.Utc),
                        IpAddress = "127.0.0.1",
                        UserAgent = "Browser A",
                        ExpiresAtUtc = new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc)
                    }
                ]
            }
        };
        var controller = CreateSessionController(new[]
        {
            new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, userId.ToString()),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-123")
        });

        var result = await controller.GetSessions(useCase);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ResponseUserSessionsJson>(okResult.Value);
        var session = Assert.Single(response.Sessions);

        Assert.Equal(userId, useCase.LastQuery!.UserId);
        Assert.Equal("session-123", useCase.LastQuery.CurrentSessionId);
        Assert.Equal("session-123", response.CurrentSid);
        Assert.Equal("session-123", session.Sid);
        Assert.Equal("127.0.0.1", session.Ip);
    }

    [Fact]
    public async Task RevokeSession_WhenRevokingCurrentSession_ShouldReturnNoContentAndDeleteCookie()
    {
        var userId = Guid.NewGuid();
        var useCase = new SpyRevokeUserSessionUseCase();
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController(new[]
        {
            new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, userId.ToString()),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-123")
        });

        var result = await controller.RevokeSession("session-123", useCase, CreateServiceProvider(authCookieOptions));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(userId, useCase.LastCommand!.UserId);
        Assert.Equal("session-123", useCase.LastCommand.SessionId);
        Assert.Contains("sid=", controller.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokeSession_WhenRevokingCurrentSessionWithWhitespace_ShouldNormalizeSessionIdAndDeleteCookie()
    {
        var userId = Guid.NewGuid();
        var useCase = new SpyRevokeUserSessionUseCase();
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController(new[]
        {
            new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, userId.ToString()),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-123")
        });

        var result = await controller.RevokeSession("  session-123  ", useCase, CreateServiceProvider(authCookieOptions));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("session-123", useCase.LastCommand!.SessionId);
        Assert.Contains("sid=", controller.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokeSession_WhenRefererIsAllowed_ShouldReturnNoContent()
    {
        var userId = Guid.NewGuid();
        var useCase = new SpyRevokeUserSessionUseCase();
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController(
            [
                new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, userId.ToString()),
                new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-current")
            ],
            csrfRequestValidator: CreateCsrfRequestValidator("https://app.authcore.dev"));

        controller.Request.Headers.Referer = "https://app.authcore.dev/seguranca/sessoes";

        var result = await controller.RevokeSession("session-other", useCase, CreateServiceProvider(authCookieOptions));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(userId, useCase.LastCommand!.UserId);
        Assert.Equal("session-other", useCase.LastCommand.SessionId);
    }

    [Fact]
    public async Task RevokeSession_WhenRevokingAnotherSession_ShouldNotDeleteCookie()
    {
        var userId = Guid.NewGuid();
        var useCase = new SpyRevokeUserSessionUseCase();
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController(new[]
        {
            new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, userId.ToString()),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-current")
        });

        var result = await controller.RevokeSession("session-other", useCase, CreateServiceProvider(authCookieOptions));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("session-other", useCase.LastCommand!.SessionId);
        Assert.True(string.IsNullOrWhiteSpace(controller.Response.Headers.SetCookie.ToString()));
    }

    [Fact]
    public async Task RevokeSession_WhenUseCaseThrowsNotFoundException_ShouldReturnNotFoundWithoutDeletingCookie()
    {
        var userId = Guid.NewGuid();
        var useCase = new ThrowingRevokeUserSessionUseCase(new NotFoundException("A sessão informada não foi encontrada para o usuário."));
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController(new[]
        {
            new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, userId.ToString()),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-current")
        });

        var result = await controller.RevokeSession("session-other", useCase, CreateServiceProvider(authCookieOptions));

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ResponseErrorJson>(notFoundResult.Value);

        Assert.Equal(["A sessão informada não foi encontrada para o usuário."], response.Errors);
        Assert.True(string.IsNullOrWhiteSpace(controller.Response.Headers.SetCookie.ToString()));
    }

    [Fact]
    public async Task LogoutAll_WhenUseCaseSucceeds_ShouldReturnNoContentAndDeleteCookie()
    {
        var userId = Guid.NewGuid();
        var useCase = new SpyLogoutAllSessionsUseCase();
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController(new[]
        {
            new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, userId.ToString()),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-123")
        });

        var result = await controller.LogoutAll(useCase, CreateServiceProvider(authCookieOptions));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(userId, useCase.LastCommand!.UserId);
        Assert.Contains("sid=", controller.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogoutAll_WhenOriginAndRefererAreMissing_ShouldReturnForbiddenResponseErrorJson()
    {
        var userId = Guid.NewGuid();
        var useCase = new SpyLogoutAllSessionsUseCase();
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var controller = CreateSessionController(
            [
                new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, userId.ToString()),
                new Claim(SessionAuthenticationDefaults.SessionIdClaimType, "session-123")
            ],
            csrfRequestValidator: CreateCsrfRequestValidator("https://app.authcore.dev"));

        var result = await controller.LogoutAll(useCase, CreateServiceProvider(authCookieOptions));

        var forbiddenResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<ResponseErrorJson>(forbiddenResult.Value);

        Assert.Equal(StatusCodes.Status403Forbidden, forbiddenResult.StatusCode);
        Assert.Equal(["A origem da requisição não é permitida."], response.Errors);
        Assert.Null(useCase.LastCommand);
    }

    [Fact]
    public async Task Login_WhenEmailRateLimitIsExceeded_ShouldReturnTooManyRequests()
    {
        var useCase = new SpyLoginSessionUseCase
        {
            Result = new AuthenticatedUserSessionResult
            {
                SessionId = "session-123",
                UserIdentifier = Guid.NewGuid(),
                Email = "blocked@authcore.dev",
                ExpiresAtUtc = new DateTime(2026, 4, 20, 15, 0, 0, DateTimeKind.Utc)
            }
        };
        var authCookieOptions = Options.Create(new AuthCookieOptions
        {
            SessionCookieName = "sid",
            Secure = false
        });
        var rateLimiter = CreateLoginRateLimiter(new LoginRateLimitOptions
        {
            MaxAttemptsPerIp = 20,
            MaxAttemptsPerEmail = 1,
            WindowMinutes = 5
        });
        var controller = CreateSessionController(loginRateLimiter: rateLimiter);

        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        _ = await controller.Login(useCase, CreateServiceProvider(authCookieOptions), new RequestSessionLoginJson
        {
            Email = "blocked@authcore.dev",
            Password = "ValidPassword#2026"
        });

        var blockedResult = await controller.Login(useCase, CreateServiceProvider(authCookieOptions), new RequestSessionLoginJson
        {
            Email = "blocked@authcore.dev",
            Password = "ValidPassword#2026"
        });

        var tooManyRequestsResult = Assert.IsType<ObjectResult>(blockedResult.Result);
        var response = Assert.IsType<ResponseErrorJson>(tooManyRequestsResult.Value);

        Assert.Equal(StatusCodes.Status429TooManyRequests, tooManyRequestsResult.StatusCode);
        Assert.Equal(["Muitas tentativas de login. Aguarde alguns minutos e tente novamente."], response.Errors);
        Assert.Equal("300", controller.Response.Headers["Retry-After"].ToString());
    }

    [Fact]
    public async Task Token_WhenIpRateLimitIsExceeded_ShouldReturnTooManyRequests()
    {
        var useCase = new SpyLoginUseCase
        {
            Result = new AuthenticatedSessionResult
            {
                AccessToken = "access-token",
                AccessTokenExpiresAtUtc = new DateTime(2026, 4, 13, 15, 0, 0, DateTimeKind.Utc),
                RefreshToken = "refresh-token",
                RefreshTokenExpiresAtUtc = new DateTime(2026, 4, 20, 15, 0, 0, DateTimeKind.Utc)
            }
        };
        var rateLimiter = CreateLoginRateLimiter(new LoginRateLimitOptions
        {
            MaxAttemptsPerIp = 1,
            MaxAttemptsPerEmail = 20,
            WindowMinutes = 5
        });
        var controller = CreateTokenController(loginRateLimiter: rateLimiter);

        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        _ = await controller.Login(useCase, new RequestLoginJson
        {
            Email = "first@authcore.dev",
            Password = "ValidPassword#2026"
        });

        var blockedResult = await controller.Login(useCase, new RequestLoginJson
        {
            Email = "second@authcore.dev",
            Password = "ValidPassword#2026"
        });

        var tooManyRequestsResult = Assert.IsType<ObjectResult>(blockedResult.Result);
        var response = Assert.IsType<ResponseErrorJson>(tooManyRequestsResult.Value);

        Assert.Equal(StatusCodes.Status429TooManyRequests, tooManyRequestsResult.StatusCode);
        Assert.Equal(["Muitas tentativas de login. Aguarde alguns minutos e tente novamente."], response.Errors);
    }

    [Fact]
    public void Build_WhenReverseProxyIsConfigured_ShouldBindForwardedHeadersOptions()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = "12345678901234567890123456789012",
            ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = "15",
            ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Auth:Cookie:SessionCookieName"] = "sid",
            ["Auth:Cookie:Secure"] = "false",
            ["ReverseProxy:KnownProxies:0"] = "10.0.0.10",
            ["ReverseProxy:KnownNetworks:0"] = "10.10.0.0/24",
            ["ReverseProxy:ForwardLimit"] = "3"
        });

        builder.Services.AddApi(builder.Configuration);

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var forwardedHeadersOptions = serviceProvider
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        Assert.Equal(3, forwardedHeadersOptions.ForwardLimit);
        Assert.Contains(forwardedHeadersOptions.KnownProxies, proxy => proxy.ToString() == "10.0.0.10");
        Assert.Contains(
            forwardedHeadersOptions.KnownNetworks,
            network => network.Prefix.ToString() == "10.10.0.0" && network.PrefixLength == 24);
    }

    [Fact]
    public async Task Build_WhenMvcIsConfigured_ShouldDiscoverAuthEndpoints()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = "12345678901234567890123456789012",
            ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = "15",
            ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Auth:Cookie:SessionCookieName"] = "sid",
            ["Auth:Cookie:Secure"] = "false"
        });

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly);

        builder.Services.AddApi(builder.Configuration);

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var actionProvider = scope.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var actions = actionProvider.ActionDescriptors.Items
            .Select(action => action.AttributeRouteInfo?.Template)
            .Where(template => !string.IsNullOrWhiteSpace(template))
            .ToList();

        Assert.Contains("api/auth/session/login", actions);
        Assert.Contains("api/auth/register", actions);
        Assert.Contains("api/auth/verify-email", actions);
        Assert.Contains("api/auth/resend-verification", actions);
        Assert.Contains("api/auth/token/login", actions);
        Assert.Contains("api/auth/token/refresh", actions);
        Assert.Contains("api/auth/token/logout", actions);
        Assert.Contains("api/auth/session/me", actions);
        Assert.Contains("api/auth/session/logout", actions);
        Assert.Contains("api/auth/session/sessions", actions);
        Assert.Contains("api/auth/session/sessions/{sid}", actions);
        Assert.Contains("api/auth/session/logout-all", actions);
        Assert.DoesNotContain("api/auth/login", actions);
        Assert.DoesNotContain("api/auth/token", actions);
        Assert.DoesNotContain("api/auth/refresh", actions);
        Assert.DoesNotContain("api/auth/me", actions);
        Assert.DoesNotContain("api/auth/logout", actions);
        Assert.DoesNotContain("api/auth/sessions", actions);
        Assert.DoesNotContain("api/auth/sessions/{sid}", actions);
        Assert.DoesNotContain("api/auth/logout-all", actions);
    }


    private static AuthController CreateAuthController()
    {
        return new AuthController(NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static SessionAuthController CreateSessionController(
        IEnumerable<Claim>? claims = null,
        ICsrfRequestValidator? csrfRequestValidator = null,
        ILoginRateLimiter? loginRateLimiter = null)
    {
        var httpContext = new DefaultHttpContext();

        if (claims is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                SessionAuthenticationDefaults.AuthenticationScheme));
        }

        return new SessionAuthController(
            csrfRequestValidator ?? new AllowAllCsrfRequestValidator(),
            loginRateLimiter ?? new AllowAllLoginRateLimiter(),
            NullLogger<SessionAuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static TokenAuthController CreateTokenController(
        IEnumerable<Claim>? claims = null,
        ILoginRateLimiter? loginRateLimiter = null)
    {
        var httpContext = new DefaultHttpContext();

        if (claims is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                SessionAuthenticationDefaults.AuthenticationScheme));
        }

        return new TokenAuthController(
            loginRateLimiter ?? new AllowAllLoginRateLimiter(),
            NullLogger<TokenAuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static ICsrfRequestValidator CreateCsrfRequestValidator(params string[] allowedOrigins)
    {
        return new CookieCsrfRequestValidator(
            NullLogger<CookieCsrfRequestValidator>.Instance,
            Options.Create(new CsrfOptions
            {
                AllowedOrigins = allowedOrigins
            }));
    }

    private static ILoginRateLimiter CreateLoginRateLimiter(LoginRateLimitOptions options)
    {
        return new InMemoryLoginRateLimiter(
            new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero)),
            Options.Create(options));
    }

    private static IServiceProvider CreateServiceProvider(IOptions<AuthCookieOptions> authCookieOptions)
    {
        return new ServiceCollection()
            .AddSingleton(authCookieOptions)
            .BuildServiceProvider();
    }

    private sealed class SpyLoginSessionUseCase : ILoginSessionUseCase
    {
        public LoginSessionCommand? LastCommand { get; private set; }

        public AuthenticatedUserSessionResult Result { get; set; } = new();

        public Task<AuthenticatedUserSessionResult> Execute(LoginSessionCommand command)
        {
            LastCommand = command;
            return Task.FromResult(Result);
        }
    }

    private sealed class ThrowingLoginSessionUseCase : ILoginSessionUseCase
    {
        /// <summary>
        /// Campo que armazena exception.
        /// </summary>
        private readonly Exception _exception;

        public ThrowingLoginSessionUseCase(Exception exception)
        {
            _exception = exception;
        }

        public Task<AuthenticatedUserSessionResult> Execute(LoginSessionCommand command)
        {
            return Task.FromException<AuthenticatedUserSessionResult>(_exception);
        }
    }

    private sealed class SpyLoginUseCase : ILoginUseCase
    {
        public LoginCommand? LastCommand { get; private set; }

        public AuthenticatedSessionResult Result { get; set; } = new();

        public Task<AuthenticatedSessionResult> Execute(LoginCommand command)
        {
            LastCommand = command;
            return Task.FromResult(Result);
        }
    }

    private sealed class SpyRegisterUserUseCase : IRegisterUserUseCase
    {
        public RegisterUserCommand? LastCommand { get; private set; }

        public RegisterUserResult Result { get; set; } = new();

        public Task<RegisterUserResult> Execute(RegisterUserCommand command)
        {
            LastCommand = command;
            return Task.FromResult(Result);
        }
    }

    private sealed class SpyVerifyEmailUseCase : IVerifyEmailUseCase
    {
        public VerifyEmailCommand? LastCommand { get; private set; }

        public Task Execute(VerifyEmailCommand command)
        {
            LastCommand = command;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingVerifyEmailUseCase : IVerifyEmailUseCase
    {
        /// <summary>
        /// Campo que armazena exception.
        /// </summary>
        private readonly Exception _exception;

        public ThrowingVerifyEmailUseCase(Exception exception)
        {
            _exception = exception;
        }

        public Task Execute(VerifyEmailCommand command)
        {
            return Task.FromException(_exception);
        }
    }

    private sealed class ThrowingResendVerificationUseCase : IResendVerificationUseCase
    {
        /// <summary>
        /// Campo que armazena exception.
        /// </summary>
        private readonly Exception _exception;

        public ThrowingResendVerificationUseCase(Exception exception)
        {
            _exception = exception;
        }

        public Task Execute(ResendVerificationCommand command)
        {
            return Task.FromException(_exception);
        }
    }

    private sealed class SpyLogoutCurrentSessionUseCase : ILogoutCurrentSessionUseCase
    {
        public LogoutCurrentSessionCommand? LastCommand { get; private set; }

        public Task Execute(LogoutCurrentSessionCommand command)
        {
            LastCommand = command;
            return Task.CompletedTask;
        }
    }

    private sealed class SpyLogoutSessionUseCase : ILogoutSessionUseCase
    {
        public LogoutSessionCommand? LastCommand { get; private set; }

        public Task Execute(LogoutSessionCommand command)
        {
            LastCommand = command;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingLogoutSessionUseCase : ILogoutSessionUseCase
    {
        /// <summary>
        /// Campo que armazena exception.
        /// </summary>
        private readonly Exception _exception;

        public ThrowingLogoutSessionUseCase(Exception exception)
        {
            _exception = exception;
        }

        public Task Execute(LogoutSessionCommand command)
        {
            return Task.FromException(_exception);
        }
    }

    private sealed class SpyGetUserSessionsUseCase : IGetUserSessionsUseCase
    {
        public GetUserSessionsQuery? LastQuery { get; private set; }

        public UserSessionsResult Result { get; set; } = new();

        public Task<UserSessionsResult> Execute(GetUserSessionsQuery query)
        {
            LastQuery = query;
            return Task.FromResult(Result);
        }
    }

    private sealed class SpyRefreshSessionUseCase : IRefreshSessionUseCase
    {
        public RefreshSessionCommand? LastCommand { get; private set; }

        public AuthenticatedSessionResult Result { get; set; } = new();

        public Task<AuthenticatedSessionResult> Execute(RefreshSessionCommand command)
        {
            LastCommand = command;
            return Task.FromResult(Result);
        }
    }

    private sealed class SpyRevokeUserSessionUseCase : IRevokeUserSessionUseCase
    {
        public RevokeUserSessionCommand? LastCommand { get; private set; }

        public Task Execute(RevokeUserSessionCommand command)
        {
            LastCommand = command;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingRevokeUserSessionUseCase : IRevokeUserSessionUseCase
    {
        /// <summary>
        /// Campo que armazena exception.
        /// </summary>
        private readonly Exception _exception;

        public ThrowingRevokeUserSessionUseCase(Exception exception)
        {
            _exception = exception;
        }

        public Task Execute(RevokeUserSessionCommand command)
        {
            return Task.FromException(_exception);
        }
    }

    private sealed class SpyLogoutAllSessionsUseCase : ILogoutAllSessionsUseCase
    {
        public LogoutAllSessionsCommand? LastCommand { get; private set; }

        public Task Execute(LogoutAllSessionsCommand command)
        {
            LastCommand = command;
            return Task.CompletedTask;
        }
    }

    private sealed class AllowAllCsrfRequestValidator : ICsrfRequestValidator
    {
        public void Validate(HttpRequest request)
        {
        }
    }

    private sealed class AllowAllLoginRateLimiter : ILoginRateLimiter
    {
        public Task<LoginRateLimitResult> TryAcquireAsync(string? ipAddress, string? email)
        {
            return Task.FromResult(LoginRateLimitResult.Allow());
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        /// <summary>
        /// Campo que armazena utc now.
        /// </summary>
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

}
