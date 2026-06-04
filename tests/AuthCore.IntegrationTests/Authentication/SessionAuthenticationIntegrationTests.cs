using System.Globalization;
using System.Security.Claims;
using AuthCore.Api;
using AuthCore.Api.Authentication;
using AuthCore.Api.Contracts.Requests;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Controllers;
using AuthCore.Api.Security;
using AuthCore.Application;
using AuthCore.Application.UseCases.Authentication.LoginSession;
using AuthCore.Application.UseCases.Authentication.LogoutAllSessions;
using AuthCore.Application.UseCases.Authentication.LogoutCurrentSession;
using AuthCore.Application.UseCases.Authentication.GetUserSessions;
using AuthCore.Application.UseCases.Authentication.RefreshBrowserSession;
using AuthCore.Application.UseCases.Authentication.RevokeUserSession;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Security.Tokens.Models;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthCore.IntegrationTests.Authentication;

/// <summary>
/// Verifica o fluxo principal de autenticação stateful por cookie.
/// </summary>
public sealed class SessionAuthenticationIntegrationTests
{
    [Fact]
    public async Task Execute_WhenLoginMeLogoutFlowRuns_ShouldInvalidateAuthenticationAfterLogout()
    {
        var userRepository = new InMemoryUserReadRepository();
        var passwordRepository = new InMemoryPasswordRepository();
        var sessionStore = new InMemorySessionStore();
        var passwordEncripter = new AlwaysValidPasswordEncripter();
        var sessionService = new FixedSessionService();
        var unitOfWork = new SpyUnitOfWork();
        var provider = BuildServiceProvider(
            userRepository,
            passwordRepository,
            sessionStore,
            passwordEncripter,
            sessionService,
            unitOfWork);
        var user = CreateVerifiedUser();
        var password = Password.Create(user.Id, "stored-password-hash", PasswordStatus.Active);

        userRepository.Store(user);
        passwordRepository.Store(password);

        await using var asyncScope = provider.CreateAsyncScope();
        var serviceProvider = asyncScope.ServiceProvider;
        var loginUseCase = serviceProvider.GetRequiredService<ILoginSessionUseCase>();
        var logoutUseCase = serviceProvider.GetRequiredService<ILogoutCurrentSessionUseCase>();
        var authCookieOptions = serviceProvider.GetRequiredService<IOptions<AuthCookieOptions>>();
        var loginController = CreateController(serviceProvider);
        var loginResult = await loginController.Login(loginUseCase, authCookieOptions, new RequestSessionLoginJson
        {
            Email = user.Email.Value,
            Password = "ValidPassword#2026"
        });
        var loginOkResult = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loginResponse = Assert.IsType<ResponseAuthenticatedUserJson>(loginOkResult.Value);
        var sessionId = ExtractCookieValue(loginController.Response.Headers.SetCookie.ToString(), "sid");

        Assert.Equal(user.UserIdentifier, loginResponse.UserId);
        Assert.Equal(user.Email.Value, loginResponse.Email);

        var storedSession = await sessionStore.GetByIdAsync(sessionId);
        Assert.NotNull(storedSession);
        var meContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            User = CreateSessionPrincipal(user, storedSession!)
        };

        var meController = CreateController(serviceProvider, meContext);
        var meResult = meController.Me();
        var meOkResult = Assert.IsType<OkObjectResult>(meResult.Result);
        var meResponse = Assert.IsType<ResponseAuthenticatedUserJson>(meOkResult.Value);

        Assert.Equal(user.UserIdentifier, meResponse.UserId);
        Assert.Equal(user.Email.Value, meResponse.Email);

        var logoutContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            User = meContext.User
        };
        var logoutController = CreateController(serviceProvider, logoutContext);
        var logoutResult = await logoutController.Logout(logoutUseCase, authCookieOptions);

        Assert.IsType<NoContentResult>(logoutResult);
        Assert.Contains(sessionId, sessionStore.RevokedSessionIds);
        Assert.Null(await sessionStore.GetByIdAsync(sessionId));
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
    }

    [Fact]
    public async Task Me_WhenSessionBelongsToPendingUser_ShouldReturnForbidden()
    {
        var userRepository = new InMemoryUserReadRepository();
        var passwordRepository = new InMemoryPasswordRepository();
        var sessionStore = new InMemorySessionStore();
        var sessionService = new FixedSessionService
        {
            UseSlidingExpiration = true
        };
        var provider = BuildServiceProvider(
            userRepository,
            passwordRepository,
            sessionStore,
            new AlwaysValidPasswordEncripter(),
            sessionService,
            new SpyUnitOfWork());
        var user = CreatePendingUser();
        var session = Session.Issue(user.Id, user.SecurityStamp, DateTime.UtcNow.AddMinutes(30), "127.0.0.1", "IntegrationTests/1.0");

        userRepository.Store(user);
        sessionStore.Store(session);

        await using var authScope = provider.CreateAsyncScope();
        var authServiceProvider = authScope.ServiceProvider;
        var authCookieOptions = authServiceProvider.GetRequiredService<IOptions<AuthCookieOptions>>().Value;
        var authenticationHandlerProvider = authServiceProvider.GetRequiredService<IAuthenticationHandlerProvider>();
        var authenticationSchemeProvider = authServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        var sessionAuthenticationScheme = await authenticationSchemeProvider.GetSchemeAsync(SessionAuthenticationDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = authServiceProvider
        };
        SetRequestCookies(httpContext, ("sid", session.SessionId));
        Assert.Equal("sid", authCookieOptions.SessionCookieName);
        Assert.True(httpContext.Request.Cookies.TryGetValue("sid", out var currentSessionId));
        Assert.Equal(session.SessionId, currentSessionId);

        var authenticationHandler = await authenticationHandlerProvider.GetHandlerAsync(
            httpContext,
            sessionAuthenticationScheme!.Name);
        var authenticateResult = await authenticationHandler!.AuthenticateAsync();

        Assert.False(authenticateResult.Succeeded);
        Assert.True(string.IsNullOrWhiteSpace(httpContext.Response.Headers.SetCookie.ToString()));
    }

    [Fact]
    public async Task Refresh_WhenSlidingExpirationIsEnabled_ShouldRenewSessionCookie()
    {
        var userRepository = new InMemoryUserReadRepository();
        var passwordRepository = new InMemoryPasswordRepository();
        var sessionStore = new InMemorySessionStore();
        var passwordEncripter = new AlwaysValidPasswordEncripter();
        var sessionService = new FixedSessionService
        {
            UseSlidingExpiration = true,
            SlidingExpiresAtUtc = DateTime.UtcNow.AddHours(8)
        };
        var provider = BuildServiceProvider(
            userRepository,
            passwordRepository,
            sessionStore,
            passwordEncripter,
            sessionService,
            new SpyUnitOfWork());
        var user = CreateVerifiedUser();
        var expectedCookieDate = new DateTimeOffset(sessionService.SlidingExpiresAtUtc)
            .ToString("ddd, dd MMM yyyy HH':'mm':'ss 'GMT'", CultureInfo.InvariantCulture);
        var password = Password.Create(user.Id, "stored-password-hash", PasswordStatus.Active);

        userRepository.Store(user);
        passwordRepository.Store(password);

        await using var authScope = provider.CreateAsyncScope();
        var authServiceProvider = authScope.ServiceProvider;
        var loginUseCase = authServiceProvider.GetRequiredService<ILoginSessionUseCase>();
        var refreshUseCase = authServiceProvider.GetRequiredService<IRefreshBrowserSessionUseCase>();
        var authCookieOptions = authServiceProvider.GetRequiredService<IOptions<AuthCookieOptions>>();
        var loginController = CreateController(authServiceProvider);
        var loginResult = await loginController.Login(loginUseCase, authCookieOptions, new RequestSessionLoginJson
        {
            Email = user.Email.Value,
            Password = "ValidPassword#2026"
        });
        _ = Assert.IsType<OkObjectResult>(loginResult.Result);
        var sessionId = ExtractCookieValue(loginController.Response.Headers.SetCookie.ToString(), "sid");

        var refreshContext = new DefaultHttpContext
        {
            RequestServices = authServiceProvider
        };
        SetRequestCookies(refreshContext, ("sid", sessionId), ("XSRF-TOKEN", "csrf-token"));
        var refreshController = CreateController(authServiceProvider, refreshContext);
        var refreshResult = await refreshController.Refresh(refreshUseCase, authCookieOptions);

        Assert.IsType<NoContentResult>(refreshResult);
        Assert.Contains($"sid={sessionId}", refreshController.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
        Assert.Contains(expectedCookieDate, refreshController.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_WhenSessionsFlowRevokesCurrentSession_ShouldInvalidateAuthenticationImmediately()
    {
        var userRepository = new InMemoryUserReadRepository();
        var passwordRepository = new InMemoryPasswordRepository();
        var sessionStore = new InMemorySessionStore();
        var passwordEncripter = new AlwaysValidPasswordEncripter();
        var sessionService = new FixedSessionService();
        var unitOfWork = new SpyUnitOfWork();
        var provider = BuildServiceProvider(
            userRepository,
            passwordRepository,
            sessionStore,
            passwordEncripter,
            sessionService,
            unitOfWork);
        var user = CreateVerifiedUser();
        var password = Password.Create(user.Id, "stored-password-hash", PasswordStatus.Active);

        userRepository.Store(user);
        passwordRepository.Store(password);

        await using var asyncScope = provider.CreateAsyncScope();
        var serviceProvider = asyncScope.ServiceProvider;
        var loginUseCase = serviceProvider.GetRequiredService<ILoginSessionUseCase>();
        var getUserSessionsUseCase = serviceProvider.GetRequiredService<IGetUserSessionsUseCase>();
        var revokeUserSessionUseCase = serviceProvider.GetRequiredService<IRevokeUserSessionUseCase>();
        var authCookieOptions = serviceProvider.GetRequiredService<IOptions<AuthCookieOptions>>();
        var authenticationHandlerProvider = serviceProvider.GetRequiredService<IAuthenticationHandlerProvider>();
        var authenticationSchemeProvider = serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        var sessionAuthenticationScheme = await authenticationSchemeProvider.GetSchemeAsync(SessionAuthenticationDefaults.AuthenticationScheme);

        var loginController = CreateController(serviceProvider);
        var loginResult = await loginController.Login(loginUseCase, authCookieOptions, new RequestSessionLoginJson
        {
            Email = user.Email.Value,
            Password = "ValidPassword#2026"
        });
        var loginOkResult = Assert.IsType<OkObjectResult>(loginResult.Result);
        _ = Assert.IsType<ResponseAuthenticatedUserJson>(loginOkResult.Value);
        var sessionId = ExtractCookieValue(loginController.Response.Headers.SetCookie.ToString(), "sid");

        var sessionsContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        SetRequestCookies(sessionsContext, ("sid", sessionId));

        var authenticationHandler = await authenticationHandlerProvider.GetHandlerAsync(
            sessionsContext,
            sessionAuthenticationScheme!.Name);
        var authenticateResult = await authenticationHandler!.AuthenticateAsync();

        Assert.True(authenticateResult.Succeeded);
        sessionsContext.User = authenticateResult.Principal!;

        var sessionsController = CreateController(serviceProvider, sessionsContext);
        var sessionsResult = await sessionsController.GetSessions(getUserSessionsUseCase);
        var sessionsOkResult = Assert.IsType<OkObjectResult>(sessionsResult.Result);
        var sessionsResponse = Assert.IsType<ResponseUserSessionsJson>(sessionsOkResult.Value);

        var storedSession = await sessionStore.GetByIdAsync(sessionId);
        Assert.NotNull(storedSession);
        Assert.Equal(storedSession!.PublicSessionId, sessionsResponse.CurrentSid);
        Assert.Contains(sessionsResponse.Sessions, session => session.Sid == storedSession.PublicSessionId);

        var revokeContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            User = authenticateResult.Principal!
        };
        var revokeController = CreateController(serviceProvider, revokeContext);
        var revokeResult = await revokeController.RevokeSession(sessionsResponse.CurrentSid, revokeUserSessionUseCase, authCookieOptions);

        Assert.IsType<NoContentResult>(revokeResult);
        Assert.Contains(sessionId, sessionStore.RevokedSessionIds);
        Assert.Contains("sid=", revokeController.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
        Assert.Null(await sessionStore.GetByIdAsync(sessionId));

        await using var reAuthenticationScope = provider.CreateAsyncScope();
        var reAuthenticationServiceProvider = reAuthenticationScope.ServiceProvider;
        var reAuthenticationHandlerProvider = reAuthenticationServiceProvider.GetRequiredService<IAuthenticationHandlerProvider>();

        var afterRevokeContext = new DefaultHttpContext
        {
            RequestServices = reAuthenticationServiceProvider
        };
        SetRequestCookies(afterRevokeContext, ("sid", sessionId));

        var authenticationHandlerAfterRevoke = await reAuthenticationHandlerProvider.GetHandlerAsync(
            afterRevokeContext,
            sessionAuthenticationScheme.Name);
        var authenticateAfterRevoke = await authenticationHandlerAfterRevoke!.AuthenticateAsync();

        Assert.False(authenticateAfterRevoke.Succeeded);
        Assert.Equal(1, unitOfWork.BegunTransactions);
        Assert.Equal(1, unitOfWork.CommittedTransactions);
    }

    [Fact]
    public async Task LogoutAll_WhenUserHasMultipleSessions_ShouldRevokeEverySessionAndDeleteCookie()
    {
        var userRepository = new InMemoryUserReadRepository();
        var passwordRepository = new InMemoryPasswordRepository();
        var sessionStore = new InMemorySessionStore();
        var provider = BuildServiceProvider(
            userRepository,
            passwordRepository,
            sessionStore,
            new AlwaysValidPasswordEncripter(),
            new FixedSessionService(),
            new SpyUnitOfWork());
        var user = CreateVerifiedUser();
        var currentSession = Session.Issue(user.Id, DateTime.UtcNow.AddMinutes(30), "127.0.0.1", "Browser A");
        var anotherSession = Session.Issue(user.Id, DateTime.UtcNow.AddMinutes(30), "127.0.0.2", "Browser B");

        userRepository.Store(user);
        sessionStore.Store(currentSession);
        sessionStore.Store(anotherSession);

        await using var authScope = provider.CreateAsyncScope();
        var serviceProvider = authScope.ServiceProvider;
        var logoutAllUseCase = serviceProvider.GetRequiredService<ILogoutAllSessionsUseCase>();
        var authCookieOptions = serviceProvider.GetRequiredService<IOptions<AuthCookieOptions>>();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, user.Id.ToString()),
                new Claim(SessionAuthenticationDefaults.SessionIdClaimType, currentSession.SessionId)
            ],
            SessionAuthenticationDefaults.AuthenticationScheme))
        };

        var controller = CreateController(serviceProvider, httpContext);
        var result = await controller.LogoutAll(logoutAllUseCase, authCookieOptions);

        Assert.IsType<NoContentResult>(result);
        Assert.Contains(currentSession.SessionId, sessionStore.RevokedSessionIds);
        Assert.Contains(anotherSession.SessionId, sessionStore.RevokedSessionIds);
        Assert.Contains("sid=", controller.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
        Assert.Empty(await sessionStore.ListByUserIdAsync(user.Id));
    }


    private static ServiceProvider BuildServiceProvider(
        InMemoryUserReadRepository userRepository,
        InMemoryPasswordRepository passwordRepository,
        InMemorySessionStore sessionStore,
        AlwaysValidPasswordEncripter passwordEncripter,
        FixedSessionService sessionService,
        SpyUnitOfWork unitOfWork)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "authcore-tests",
                ["Authentication:Jwt:Audience"] = "authcore-tests",
                ["Authentication:Jwt:SigningKey"] = "AuthCore-Tests-SigningKey-2026-Strong!",
                ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = "5",
                ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
                ["Authentication:Jwt:ClockSkewSeconds"] = "60",
                ["Auth:Cookie:SessionCookieName"] = "sid",
                ["Auth:Cookie:AccessTokenCookieName"] = "at",
                ["Auth:Cookie:Secure"] = "false",
                ["Auth:Csrf:SigningKey"] = "tests-csrf-signing-key-2026"
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IOptions<AuthCookieOptions>>(Options.Create(new AuthCookieOptions
            {
                SessionCookieName = "sid",
                AccessTokenCookieName = "at",
                Secure = false
            }))
            .AddSingleton<IOptions<CsrfOptions>>(Options.Create(new CsrfOptions
            {
                CookieName = "XSRF-TOKEN",
                HeaderName = "X-CSRF-TOKEN",
                SigningKey = "tests-csrf-signing-key-2026"
            }))
            .AddSingleton<IUserReadRepository>(userRepository)
            .AddSingleton<IPasswordRepository>(passwordRepository)
            .AddSingleton<IDurableSessionRepository>(new InMemoryDurableSessionRepository())
            .AddSingleton<ISessionStore>(sessionStore)
            .AddSingleton<IPasswordEncripter>(passwordEncripter)
            .AddSingleton<IAccessTokenGenerator, FixedAccessTokenGenerator>()
            .AddSingleton<ISessionIdentifierHasher, InMemorySessionIdentifierHasher>()
            .AddSingleton<ISessionService>(sessionService)
            .AddSingleton<IUnitOfWork>(unitOfWork)
            .AddApi(configuration)
            .AddSingleton<ILoginRateLimiter, AllowAllLoginRateLimiter>()
            .AddScoped<ICsrfRequestValidator, AllowAllCsrfRequestValidator>()
            .AddApplication()
            .BuildServiceProvider();
    }

    private static SessionAuthController CreateController(IServiceProvider serviceProvider, HttpContext? httpContext = null)
    {
        var resolvedHttpContext = httpContext ?? new DefaultHttpContext();
        resolvedHttpContext.RequestServices = serviceProvider;

        return new SessionAuthController(
            new AuthenticatedSessionContext(resolvedHttpContext.User),
            serviceProvider.GetRequiredService<ICsrfRequestValidator>(),
            new AllowAllCsrfTokenService(),
            serviceProvider.GetRequiredService<ILoginRateLimiter>(),
            serviceProvider.GetRequiredService<ILogger<SessionAuthController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = resolvedHttpContext
            }
        };
    }

    private static string ExtractCookieValue(string setCookieHeader, string cookieName)
    {
        var prefix = $"{cookieName}=";
        var cookieSegment = setCookieHeader
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .First(segment => segment.StartsWith(prefix, StringComparison.Ordinal));

        return cookieSegment[prefix.Length..];
    }

    private static ClaimsPrincipal CreateSessionPrincipal(User user, Session session)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.UserIdentifier.ToString()),
            new Claim(ClaimTypes.Email, user.Email.Value),
            new Claim("sub", user.UserIdentifier.ToString()),
            new Claim("user_identifier", user.UserIdentifier.ToString()),
            new Claim(SessionAuthenticationDefaults.InternalUserIdClaimType, user.Id.ToString()),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, session.SessionId),
            new Claim(SessionAuthenticationDefaults.PublicSessionIdClaimType, session.PublicSessionId),
            new Claim(SessionAuthenticationDefaults.UserStatusClaimType, user.Status.ToString()),
            new Claim(SessionAuthenticationDefaults.UserIsActiveClaimType, user.IsActive.ToString())
        ],
        SessionAuthenticationDefaults.AuthenticationScheme));
    }

    private static void SetRequestCookies(HttpContext httpContext, params (string Name, string Value)[] cookies)
    {
        var cookieDictionary = cookies.ToDictionary(cookie => cookie.Name, cookie => cookie.Value, StringComparer.Ordinal);
        var cookieHeader = string.Join("; ", cookieDictionary.Select(cookie => $"{cookie.Key}={cookie.Value}"));

        httpContext.Request.Headers.Cookie = cookieHeader;
        httpContext.Features.Set<IRequestCookiesFeature>(new StaticRequestCookiesFeature(cookieDictionary));
    }

    private static User CreateVerifiedUser()
    {
        var user = User.Register(
            firstName: "Auth",
            lastName: "Core",
            email: "session.integration@authcore.dev",
            contact: "11999999999",
            role: Role.User);

        user.VerifyEmail(new DateTime(2026, 4, 14, 9, 0, 0, DateTimeKind.Utc));

        return user;
    }

    private static User CreatePendingUser()
    {
        return User.Register(
            firstName: "Pending",
            lastName: "User",
            email: "session.pending@authcore.dev",
            contact: "11999999999",
            role: Role.User);
    }

    private sealed class InMemoryUserReadRepository : IUserReadRepository
    {
        /// <summary>
        /// Campo que armazena users by id.
        /// </summary>
        private readonly Dictionary<Guid, User> _usersById = [];
        /// <summary>
        /// Campo que armazena users by email.
        /// </summary>
        private readonly Dictionary<string, User> _usersByEmail = [];
        /// <summary>
        /// Campo que armazena users by identifier.
        /// </summary>
        private readonly Dictionary<Guid, User> _usersByIdentifier = [];

        public Task<User?> GetByIdAsync(Guid userId)
        {
            _usersById.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<User?> GetByUserIdentifierAsync(Guid userIdentifier)
        {
            _usersByIdentifier.TryGetValue(userIdentifier, out var user);
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            _usersByEmail.TryGetValue(email.Trim().ToLowerInvariant(), out var user);
            return Task.FromResult(user);
        }

        public void Store(User user)
        {
            _usersById[user.Id] = user;
            _usersByEmail[user.Email.Value] = user;
            _usersByIdentifier[user.UserIdentifier] = user;
        }
    }

    private sealed class AllowAllCsrfRequestValidator : ICsrfRequestValidator
    {
        public void Validate(HttpRequest request)
        {
        }
    }

    private sealed class AllowAllCsrfTokenService : ICsrfTokenService
    {
        public string Generate(string sessionId)
        {
            return "csrf-token";
        }

        public bool IsValid(string sessionId, string token)
        {
            return true;
        }
    }

    private sealed class AllowAllLoginRateLimiter : ILoginRateLimiter
    {
        public Task<LoginRateLimitResult> TryAcquireAsync(string? ipAddress, string? email)
        {
            return Task.FromResult(LoginRateLimitResult.Allow());
        }
    }

    private sealed class InMemoryPasswordRepository : IPasswordRepository
    {
        /// <summary>
        /// Campo que armazena passwords by user id.
        /// </summary>
        private readonly Dictionary<Guid, Password> _passwordsByUserId = [];

        public Task AddAsync(Password password)
        {
            _passwordsByUserId[password.UserId] = password;
            return Task.CompletedTask;
        }

        public Task<Password?> GetByUserIdAsync(Guid userId)
        {
            _passwordsByUserId.TryGetValue(userId, out var password);
            return Task.FromResult(password);
        }

        public Task UpdateAsync(Password password)
        {
            _passwordsByUserId[password.UserId] = password;
            return Task.CompletedTask;
        }

        public void Store(Password password)
        {
            _passwordsByUserId[password.UserId] = password;
        }
    }

    private sealed class InMemorySessionStore : ISessionStore
    {
        /// <summary>
        /// Campo que armazena sessions by id.
        /// </summary>
        private readonly Dictionary<string, Session> _sessionsById = [];

        public List<string> RevokedSessionIds { get; } = [];

        public Task SaveAsync(Session session)
        {
            _sessionsById[session.SessionId] = session;
            return Task.CompletedTask;
        }

        public void Store(Session session)
        {
            _sessionsById[session.SessionId] = session;
        }

        public Task<Session?> GetByIdAsync(string sessionId)
        {
            _sessionsById.TryGetValue(sessionId.Trim(), out var session);
            return Task.FromResult(session);
        }

        public Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId)
        {
            IReadOnlyCollection<Session> sessions = _sessionsById.Values
                .Where(session => session.UserId == userId)
                .ToArray();

            return Task.FromResult(sessions);
        }

        public Task RevokeAsync(string sessionId)
        {
            RevokedSessionIds.Add(sessionId);
            _sessionsById.Remove(sessionId);
            return Task.CompletedTask;
        }

        public Task RevokeAllAsync(Guid userId)
        {
            foreach (var session in _sessionsById.Values.Where(session => session.UserId == userId).ToArray())
            {
                RevokedSessionIds.Add(session.SessionId);
                _sessionsById.Remove(session.SessionId);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysValidPasswordEncripter : IPasswordEncripter
    {
        public string Encrypt(string password)
        {
            return $"hashed::{password}";
        }

        public bool IsValid(string password, string passwordHash)
        {
            return true;
        }
    }

    private sealed class FixedAccessTokenGenerator : IAccessTokenGenerator
    {
        public AccessTokenResult Generate(User user, Session? session = null)
        {
            return new AccessTokenResult
            {
                Token = "integration-access-token",
                TokenId = Guid.NewGuid(),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            };
        }
    }

    private sealed class InMemorySessionIdentifierHasher : ISessionIdentifierHasher
    {
        public string ComputeHash(SessionIdentifier identifier)
        {
            return $"{identifier.Value}-hash";
        }
    }

    private sealed class FixedSessionService : ISessionService
    {
        public bool UseSlidingExpiration { get; set; }

        public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(8);

        public DateTime SlidingExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(9);

        public TimeSpan LastSeenUpdateInterval { get; set; } = TimeSpan.FromMinutes(1);

        public DateTime GetExpiresAtUtc()
        {
            return ExpiresAtUtc;
        }

        public DateTime GetSlidingExpiresAtUtc(DateTime referenceAtUtc)
        {
            return SlidingExpiresAtUtc;
        }

        public TimeSpan GetLastSeenUpdateInterval()
        {
            return LastSeenUpdateInterval;
        }
    }

    private sealed class StaticRequestCookiesFeature : IRequestCookiesFeature
    {
        public StaticRequestCookiesFeature(IDictionary<string, string> cookies)
        {
            Cookies = new StaticRequestCookieCollection(cookies);
        }

        public IRequestCookieCollection Cookies { get; set; }
    }

    private sealed class StaticRequestCookieCollection : IRequestCookieCollection
    {
        private readonly Dictionary<string, string> _cookies;

        public StaticRequestCookieCollection(IDictionary<string, string> cookies)
        {
            _cookies = new Dictionary<string, string>(cookies, StringComparer.Ordinal);
        }

        public string this[string key] => _cookies.TryGetValue(key, out var value) ? value : string.Empty;

        public int Count => _cookies.Count;

        public ICollection<string> Keys => _cookies.Keys;

        public bool ContainsKey(string key)
        {
            return _cookies.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _cookies.GetEnumerator();
        }

        public bool TryGetValue(string key, out string value)
        {
            return _cookies.TryGetValue(key, out value!);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class InMemoryDurableSessionRepository : IDurableSessionRepository
    {
        private readonly Dictionary<string, Session> _sessionsByHash = [];

        public Task AddAsync(Session session)
        {
            _sessionsByHash[$"{session.SessionId}-hash"] = session;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Session session)
        {
            _sessionsByHash[$"{session.SessionId}-hash"] = session;
            return Task.CompletedTask;
        }

        public Task<Session?> GetByIdentifierHashAsync(string sessionIdentifierHash, SessionIdentifier identifier)
        {
            _sessionsByHash.TryGetValue(sessionIdentifierHash.Trim(), out var session);
            return Task.FromResult(session);
        }

        public Task<Session?> GetByPublicSessionIdAsync(string publicSessionId)
        {
            var session = _sessionsByHash.Values.FirstOrDefault(current =>
                string.Equals(current.PublicSessionId, publicSessionId.Trim(), StringComparison.Ordinal));

            return Task.FromResult(session);
        }

        public Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId)
        {
            IReadOnlyCollection<Session> sessions = _sessionsByHash.Values
                .Where(session => session.UserId == userId)
                .ToArray();

            return Task.FromResult(sessions);
        }

        public Task RevokeActiveByUserIdAsync(Guid userId, SessionRevocationReason reason, DateTime revokedAtUtc)
        {
            foreach (var session in _sessionsByHash.Values.Where(current => current.UserId == userId).ToArray())
                _sessionsByHash[$"{session.SessionId}-hash"] = session.Revoke(reason, revokedAtUtc);

            return Task.CompletedTask;
        }
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int BegunTransactions { get; private set; }

        public int CommittedTransactions { get; private set; }

        public int RolledBackTransactions { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BegunTransactions++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommittedTransactions++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RolledBackTransactions++;
            return Task.CompletedTask;
        }
    }

}
