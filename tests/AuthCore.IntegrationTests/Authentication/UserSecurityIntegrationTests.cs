using AuthCore.Api;
using AuthCore.Api.Security;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Api.Controllers;
using AuthCore.Application;
using AuthCore.Application.UseCases.Users.ChangePassword;
using AuthCore.Application.UseCases.Users.DeleteUser;
using AuthCore.Application.UseCases.Users.GetUserProfile;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure;
using AuthCore.IntegrationTests.Passports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthCore.IntegrationTests.Authentication;

/// <summary>
/// Verifica a revogação de sessões em eventos críticos e o uso do Bearer em fluxo protegido.
/// </summary>
public sealed class UserSecurityIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    /// <summary>
    /// Campo que armazena fixture.
    /// </summary>
    private readonly PostgreSqlIntegrationFixture _fixture;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="fixture">Fixture compartilhada de integração com PostgreSQL.</param>
    public UserSecurityIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifica se a alteração de senha revoga apenas refresh tokens ainda ativos do usuário.
    /// </summary>
    [Fact]
    public async Task Execute_WhenPasswordChangesSuccessfully_ShouldRevokeActiveRefreshTokens()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var provider = BuildApplicationServiceProvider(_fixture.DatabaseConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var durableSessionRepository = scope.ServiceProvider.GetRequiredService<IDurableSessionRepository>();
        var passwordRepository = scope.ServiceProvider.GetRequiredService<IPasswordRepository>();
        var refreshTokenRepository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var passwordEncripter = scope.ServiceProvider.GetRequiredService<IPasswordEncripter>();
        var useCase = scope.ServiceProvider.GetRequiredService<IChangePasswordUseCase>();
        var nowUtc = new DateTime(2026, 4, 14, 12, 0, 0, DateTimeKind.Utc);
        var currentPassword = "CurrentPassword#2026";
        var newPassword = "NewPassword#2026";
        var user = CreateVerifiedUser($"change-password.{Guid.NewGuid():N}@authcore.dev");
        var anotherUser = CreateVerifiedUser($"other-change-password.{Guid.NewGuid():N}@authcore.dev");
        var previousSecurityStamp = user.SecurityStamp;
        var password = Password.Create(user.Id, passwordEncripter.Encrypt(currentPassword), PasswordStatus.Active);
        var activeToken = RefreshToken.IssueInitial(user.Id, $"active-hash-{Guid.NewGuid():N}", nowUtc.AddDays(7));
        var consumedToken = RefreshToken.IssueInitial(user.Id, $"consumed-hash-{Guid.NewGuid():N}", nowUtc.AddDays(7))
            .Consume(Guid.NewGuid(), nowUtc.AddMinutes(-10));
        var otherUserToken = RefreshToken.IssueInitial(anotherUser.Id, $"other-user-hash-{Guid.NewGuid():N}", nowUtc.AddDays(7));
        var activeSession = Session.Issue(user.Id, user.SecurityStamp, nowUtc.AddDays(7), "127.0.0.1", "Browser A");
        var anotherUserSession = Session.Issue(anotherUser.Id, anotherUser.SecurityStamp, nowUtc.AddDays(7), "127.0.0.2", "Browser B");

        await userRepository.AddAsync(user);
        await userRepository.AddAsync(anotherUser);
        await passwordRepository.AddAsync(password);
        await refreshTokenRepository.AddAsync(activeToken);
        await refreshTokenRepository.AddAsync(consumedToken);
        await refreshTokenRepository.AddAsync(otherUserToken);
        await durableSessionRepository.AddAsync(activeSession);
        await durableSessionRepository.AddAsync(anotherUserSession);

        await useCase.Execute(new ChangePasswordCommand
        {
            UserIdentifier = user.UserIdentifier,
            CurrentPassword = currentPassword,
            NewPassword = newPassword,
            ConfirmNewPassword = newPassword
        });

        var updatedPassword = await passwordRepository.GetByUserIdAsync(user.Id);
        var updatedUser = await scope.ServiceProvider.GetRequiredService<IUserReadRepository>().GetByUserIdentifierAsync(user.UserIdentifier);
        var persistedActiveToken = await refreshTokenRepository.GetByHashAsync(activeToken.TokenHash);
        var persistedConsumedToken = await refreshTokenRepository.GetByHashAsync(consumedToken.TokenHash);
        var persistedOtherUserToken = await refreshTokenRepository.GetByHashAsync(otherUserToken.TokenHash);
        var persistedActiveSession = await durableSessionRepository.GetByPublicSessionIdAsync(activeSession.PublicSessionId);
        var persistedAnotherUserSession = await durableSessionRepository.GetByPublicSessionIdAsync(anotherUserSession.PublicSessionId);

        Assert.NotNull(updatedPassword);
        Assert.NotNull(updatedUser);
        Assert.True(passwordEncripter.IsValid(newPassword, updatedPassword!.Value));
        Assert.Equal(PasswordStatus.Active, updatedPassword.Status);
        Assert.NotEqual(previousSecurityStamp, updatedUser!.SecurityStamp);
        Assert.NotNull(persistedActiveToken);
        Assert.Equal("password-changed", persistedActiveToken!.RevocationReason);
        Assert.NotNull(persistedActiveToken.RevokedAtUtc);
        Assert.NotNull(persistedConsumedToken);
        Assert.Null(persistedConsumedToken!.RevokedAtUtc);
        Assert.NotNull(persistedOtherUserToken);
        Assert.Null(persistedOtherUserToken!.RevokedAtUtc);
        Assert.NotNull(persistedActiveSession);
        Assert.Equal(SessionStatus.Revoked, persistedActiveSession!.Status);
        Assert.Equal(SessionRevocationReason.PasswordChanged, persistedActiveSession.RevocationReason);
        Assert.NotNull(persistedAnotherUserSession);
        Assert.Equal(SessionStatus.Active, persistedAnotherUserSession!.Status);
    }

    /// <summary>
    /// Verifica se a exclusão desativa o usuário e revoga refresh tokens ainda ativos.
    /// </summary>
    [Fact]
    public async Task Execute_WhenUserIsDeleted_ShouldDeactivatePasswordAndRevokeActiveRefreshTokens()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var provider = BuildApplicationServiceProvider(_fixture.DatabaseConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var userReadRepository = scope.ServiceProvider.GetRequiredService<IUserReadRepository>();
        var passwordRepository = scope.ServiceProvider.GetRequiredService<IPasswordRepository>();
        var refreshTokenRepository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var passwordEncripter = scope.ServiceProvider.GetRequiredService<IPasswordEncripter>();
        var useCase = scope.ServiceProvider.GetRequiredService<IDeleteUserUseCase>();
        var nowUtc = new DateTime(2026, 4, 14, 15, 0, 0, DateTimeKind.Utc);
        var user = CreateVerifiedUser($"delete-user.{Guid.NewGuid():N}@authcore.dev");
        var password = Password.Create(user.Id, passwordEncripter.Encrypt("CurrentPassword#2026"), PasswordStatus.Active);
        var activeToken = RefreshToken.IssueInitial(user.Id, $"delete-active-hash-{Guid.NewGuid():N}", nowUtc.AddDays(7));
        var expiredToken = RefreshToken.IssueInitial(user.Id, $"delete-expired-hash-{Guid.NewGuid():N}", nowUtc.AddMinutes(-1));

        await userRepository.AddAsync(user);
        await passwordRepository.AddAsync(password);
        await refreshTokenRepository.AddAsync(activeToken);
        await refreshTokenRepository.AddAsync(expiredToken);

        await useCase.Execute(new DeleteUserCommand
        {
            UserIdentifier = user.UserIdentifier
        });

        var persistedUser = await userReadRepository.GetByUserIdentifierAsync(user.UserIdentifier);
        var updatedPassword = await passwordRepository.GetByUserIdAsync(user.Id);
        var persistedActiveToken = await refreshTokenRepository.GetByHashAsync(activeToken.TokenHash);
        var persistedExpiredToken = await refreshTokenRepository.GetByHashAsync(expiredToken.TokenHash);

        Assert.NotNull(persistedUser);
        Assert.False(persistedUser!.IsActive);
        Assert.NotNull(updatedPassword);
        Assert.Equal(PasswordStatus.Deactivated, updatedPassword!.Status);
        Assert.NotNull(persistedActiveToken);
        Assert.Equal("user-deactivated", persistedActiveToken!.RevocationReason);
        Assert.NotNull(persistedActiveToken.RevokedAtUtc);
        Assert.NotNull(persistedExpiredToken);
        Assert.Null(persistedExpiredToken!.RevokedAtUtc);
    }

    /// <summary>
    /// Verifica se um Bearer emitido pela infraestrutura alcança um fluxo protegido do controller.
    /// </summary>
    [Fact]
    public async Task GetUserProfile_WhenBearerTokenIsValid_ShouldReturnProtectedProfileResponse()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var provider = BuildApplicationServiceProvider(_fixture.DatabaseConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var authenticatedUserAccessValidator = scope.ServiceProvider.GetRequiredService<IAuthenticatedUserAccessValidator>();
        var accessTokenGenerator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();
        var authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var useCase = scope.ServiceProvider.GetRequiredService<IGetUserProfileUseCase>();
        var user = CreateVerifiedUser($"bearer-profile.{Guid.NewGuid():N}@authcore.dev");
        var accessToken = accessTokenGenerator.Generate(user);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        var controller = new UserController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        await userRepository.AddAsync(user);

        httpContext.Request.Headers.Authorization = $"Bearer {accessToken.Token}";

        var authenticateResult = await authenticationService.AuthenticateAsync(
            httpContext,
            JwtBearerDefaults.AuthenticationScheme);

        Assert.True(authenticateResult.Succeeded);
        Assert.NotNull(authenticateResult.Principal);

        httpContext.User = authenticateResult.Principal!;

        var result = await controller.GetUserProfile(useCase, authenticatedUserAccessValidator);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ResponseUserProfileJson>(okResult.Value);

        Assert.Equal(user.Email.Value, response.Email);
        Assert.Equal(user.FullName, response.FullName);
        Assert.Equal(user.Role.ToString(), response.Role);
        Assert.True(response.IsEmailVerified);
    }

    /// <summary>
    /// Verifica se o bearer nega acesso quando o token carrega um usuário pendente de verificação.
    /// </summary>
    [Fact]
    public async Task GetUserProfile_WhenBearerTokenRepresentsPendingEmailVerification_ShouldThrowForbiddenException()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var provider = BuildApplicationServiceProvider(_fixture.DatabaseConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var authenticatedUserAccessValidator = scope.ServiceProvider.GetRequiredService<IAuthenticatedUserAccessValidator>();
        var accessTokenGenerator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();
        var authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var pendingUser = User.Register(
            firstName: "Pending",
            lastName: "User",
            email: $"pending-bearer.{Guid.NewGuid():N}@authcore.dev",
            contact: "11999999999",
            role: Role.User);
        var accessToken = accessTokenGenerator.Generate(pendingUser);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        var controller = new UserController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
        var useCase = new SpyGetUserProfileUseCase();

        await userRepository.AddAsync(pendingUser);

        httpContext.Request.Headers.Authorization = $"Bearer {accessToken.Token}";

        var authenticateResult = await authenticationService.AuthenticateAsync(
            httpContext,
            JwtBearerDefaults.AuthenticationScheme);

        Assert.True(authenticateResult.Succeeded);
        Assert.NotNull(authenticateResult.Principal);

        httpContext.User = authenticateResult.Principal!;

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetUserProfile(useCase, authenticatedUserAccessValidator));

        Assert.Equal("O usuário precisa verificar o e-mail antes de autenticar.", exception.Message);
        Assert.False(useCase.WasCalled);
    }

    /// <summary>
    /// Verifica se o bearer nega acesso quando o usuário é desativado após a emissão do token.
    /// </summary>
    [Fact]
    public async Task GetUserProfile_WhenUserIsDeactivatedAfterTokenIssuance_ShouldThrowForbiddenException()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var provider = BuildApplicationServiceProvider(_fixture.DatabaseConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var authenticatedUserAccessValidator = scope.ServiceProvider.GetRequiredService<IAuthenticatedUserAccessValidator>();
        var accessTokenGenerator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();
        var authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var user = CreateVerifiedUser($"deactivated-bearer.{Guid.NewGuid():N}@authcore.dev");
        var accessToken = accessTokenGenerator.Generate(user);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        var controller = new UserController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
        var useCase = new SpyGetUserProfileUseCase();

        await userRepository.AddAsync(user);
        user.Deactivate();
        await userRepository.UpdateAsync(user);

        httpContext.Request.Headers.Authorization = $"Bearer {accessToken.Token}";

        var authenticateResult = await authenticationService.AuthenticateAsync(
            httpContext,
            JwtBearerDefaults.AuthenticationScheme);

        Assert.True(authenticateResult.Succeeded);
        Assert.NotNull(authenticateResult.Principal);

        httpContext.User = authenticateResult.Principal!;

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetUserProfile(useCase, authenticatedUserAccessValidator));

        Assert.Equal("O usuário não pode autenticar no momento.", exception.Message);
        Assert.False(useCase.WasCalled);
    }


    /// <summary>
    /// Operação para criar um provider completo de serviços da API para os testes.
    /// </summary>
    /// <param name="connectionString">String de conexão do banco do teste.</param>
    /// <returns>Provider configurado com API, Application e Infrastructure.</returns>
    private static ServiceProvider BuildApplicationServiceProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = connectionString,
                ["Database:Migrations:AutoMigrateOnStartup"] = "false",
                ["Database:Migrations:EnsureDatabaseCreated"] = "false",
                ["Database:Migrations:AdminDatabase"] = "postgres",
                ["Authentication:Jwt:Issuer"] = "authcore-tests",
                ["Authentication:Jwt:Audience"] = "authcore-tests",
                ["Authentication:Jwt:SigningKey"] = "AuthCore-Tests-SigningKey-2026-Strong!",
                ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = "5",
                ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
                ["Authentication:Jwt:ClockSkewSeconds"] = "60",
                ["Redis:ConnectionString"] = "localhost:6379",
                ["Redis:KeyPrefix"] = "authcore-tests",
                ["RabbitMq:Host"] = "localhost",
                ["RabbitMq:Port"] = "5672",
                ["RabbitMq:VirtualHost"] = "/",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["RabbitMq:Exchange"] = "notification.requests",
                ["RabbitMq:RoutingKey"] = "notification.email.requested",
                ["RabbitMq:Queue"] = "notification.email.requests",
                ["RabbitMq:DeadLetterQueue"] = "notification.email.requests.dlq"
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddApi(configuration)
            .AddInfrastructure(configuration)
            .AddApplication()
            .BuildServiceProvider();
    }

    /// <summary>
    /// Operação para criar um usuário verificado apto a autenticar.
    /// </summary>
    /// <param name="email">E-mail do usuário.</param>
    /// <returns>Usuário verificado.</returns>
    private static User CreateVerifiedUser(string email)
    {
        var user = User.Register(
            firstName: "Auth",
            lastName: "Core",
            email: email,
            contact: "11999999999",
            role: Role.User);

        user.VerifyEmail(new DateTime(2026, 4, 14, 9, 0, 0, DateTimeKind.Utc));

        return user;
    }

    private sealed class SpyGetUserProfileUseCase : IGetUserProfileUseCase
    {
        public bool WasCalled { get; private set; }

        public Task<GetUserProfileResult> Execute(GetUserProfileQuery query)
        {
            WasCalled = true;
            throw new InvalidOperationException("O caso de uso não deveria ser chamado.");
        }
    }

}
