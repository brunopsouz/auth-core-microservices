using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthCore.Api;
using AuthCore.Api.Controllers;
using AuthCore.Application;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Security.Tokens;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users;
using AuthCore.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthCore.IntegrationTests.Authentication;

/// <summary>
/// Verifica a emissão e validação do JWT no pipeline autenticado.
/// </summary>
public sealed class JwtAuthenticationIntegrationTests
{
    /// <summary>
    /// Verifica se o serviço de refresh token gera material consistente.
    /// </summary>
    [Fact]
    public async Task Create_WhenRefreshTokenServiceIsResolved_ShouldGenerateConsistentMaterial()
    {
        await using var app = BuildApplication();
        await using var scope = app.Services.CreateAsyncScope();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        var refreshTokenMaterial = refreshTokenService.Create();

        Assert.False(string.IsNullOrWhiteSpace(refreshTokenMaterial.Token));
        Assert.Equal(64, refreshTokenMaterial.Hash.Length);
        Assert.Equal(refreshTokenMaterial.Hash, refreshTokenService.ComputeHash(refreshTokenMaterial.Token));
    }

    /// <summary>
    /// Verifica se um JWT emitido pela infraestrutura autentica no esquema Bearer registrado.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenTokenIsGeneratedByInfrastructure_ShouldAuthenticateBearerRequest()
    {
        await using var app = BuildApplication();
        await using var scope = app.Services.CreateAsyncScope();
        var accessTokenGenerator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();
        var authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var authenticatedUser = CreateVerifiedUser();
        var accessTokenResult = accessTokenGenerator.Generate(authenticatedUser);
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(accessTokenResult.Token);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };

        httpContext.Request.Headers.Authorization = $"Bearer {accessTokenResult.Token}";

        var authenticateResult = await authenticationService.AuthenticateAsync(
            httpContext,
            JwtBearerDefaults.AuthenticationScheme);

        Assert.True(authenticateResult.Succeeded);
        Assert.NotNull(authenticateResult.Principal);
        Assert.Equal(authenticatedUser.UserIdentifier.ToString(), jwtToken.Subject);
        Assert.Equal(authenticatedUser.UserIdentifier.ToString(), GetClaimValue(jwtToken.Claims, ClaimTypes.NameIdentifier, "nameid"));
        Assert.Equal(authenticatedUser.Role.ToString(), GetClaimValue(jwtToken.Claims, ClaimTypes.Role, "role"));
        Assert.Equal(authenticatedUser.Email.Value, GetClaimValue(jwtToken.Claims, JwtRegisteredClaimNames.Email, ClaimTypes.Email));
        Assert.Equal(authenticatedUser.Status.ToString(), GetClaimValue(jwtToken.Claims, AuthTokenClaimTypes.UserStatus));
        Assert.Equal(authenticatedUser.IsActive.ToString(), GetClaimValue(jwtToken.Claims, AuthTokenClaimTypes.UserIsActive));
        Assert.Equal(accessTokenResult.TokenId.ToString(), GetClaimValue(jwtToken.Claims, JwtRegisteredClaimNames.Jti));
        Assert.Equal(authenticatedUser.UserIdentifier.ToString(), authenticateResult.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    /// <summary>
    /// Verifica se a emissao com sessao inclui a claim sid e nao expõe o security stamp bruto.
    /// </summary>
    [Fact]
    public async Task Generate_WhenSessionIsProvided_ShouldIncludeSidWithoutExposingSecurityStamp()
    {
        await using var app = BuildApplication();
        await using var scope = app.Services.CreateAsyncScope();
        var accessTokenGenerator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();
        var authenticatedUser = CreateVerifiedUser();
        var session = Session.Issue(
            authenticatedUser.Id,
            authenticatedUser.SecurityStamp,
            DateTime.UtcNow.AddHours(8),
            "127.0.0.1",
            "JwtIntegrationTests/1.0");

        var accessTokenResult = accessTokenGenerator.Generate(authenticatedUser, session);
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(accessTokenResult.Token);

        Assert.Equal(session.SessionId, GetClaimValue(jwtToken.Claims, AuthTokenClaimTypes.SessionId));
        Assert.DoesNotContain(jwtToken.Claims, claim => claim.Value == authenticatedUser.SecurityStamp.Value);
        Assert.DoesNotContain(jwtToken.Claims, claim => claim.Type == "sst");
    }

    /// <summary>
    /// Verifica se a expiracao curta configurada do access token e respeitada.
    /// </summary>
    [Fact]
    public async Task Generate_WhenTokenIsIssued_ShouldRespectShortConfiguredExpiration()
    {
        await using var app = BuildApplication();
        await using var scope = app.Services.CreateAsyncScope();
        var accessTokenGenerator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();
        var authenticatedUser = CreateVerifiedUser();

        var beforeGenerationUtc = DateTime.UtcNow;
        var accessTokenResult = accessTokenGenerator.Generate(authenticatedUser);
        var afterGenerationUtc = DateTime.UtcNow;

        Assert.InRange(
            accessTokenResult.ExpiresAtUtc,
            beforeGenerationUtc.AddMinutes(5).AddSeconds(-5),
            afterGenerationUtc.AddMinutes(5).AddSeconds(5));
    }

    /// <summary>
    /// Verifica se a configuracao insegura do JWT falha cedo no bootstrap.
    /// </summary>
    /// <param name="signingKey">Chave de assinatura configurada.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short-key")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("QUJDREVGR0hJSktMTU5PUFFSU1RVVldY")]
    public void BuildApplication_WhenJwtSigningKeyIsInsecure_ShouldFailFast(string? signingKey)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BuildApplication(signingKey: signingKey));

        Assert.Contains("JWT", exception.Message, StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Operação para criar a aplicação usada nos testes de autenticação.
    /// </summary>
    /// <returns>Aplicação com as dependências registradas.</returns>
    private static WebApplication BuildApplication(
        string? signingKey = "AuthCore-Tests-SigningKey-2026-Strong!",
        string accessTokenLifetimeMinutes = "5")
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSql"] = "Host=localhost;Port=5432;Database=auth_core_tests;Username=postgres;Password=postgres",
            ["Database:Migrations:AutoMigrateOnStartup"] = "false",
            ["Database:Migrations:EnsureDatabaseCreated"] = "false",
            ["Database:Migrations:AdminDatabase"] = "postgres",
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = signingKey,
            ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = accessTokenLifetimeMinutes,
            ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Auth:Csrf:SigningKey"] = "tests-csrf-signing-key-2026",
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
        });

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(UserController).Assembly);

        builder.Services.AddApi(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApplication();

        return builder.Build();
    }

    /// <summary>
    /// Operação para criar um usuário apto a receber tokens.
    /// </summary>
    /// <returns>Usuário autenticável para os testes.</returns>
    private static User CreateVerifiedUser()
    {
        var user = User.Register(
            firstName: "Auth",
            lastName: "Core",
            email: "jwt.auth@example.com",
            contact: "11999999999",
            role: Role.User);

        user.VerifyEmail(new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc));

        return user;
    }

    /// <summary>
    /// Operação para obter o valor da claim considerando aliases conhecidos.
    /// </summary>
    /// <param name="claims">Claims presentes no token.</param>
    /// <param name="claimTypes">Tipos aceitos para a claim.</param>
    /// <returns>Valor encontrado para a claim.</returns>
    private static string GetClaimValue(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var claimValue = claims.FirstOrDefault(claim => claim.Type == claimType)?.Value;

            if (!string.IsNullOrWhiteSpace(claimValue))
                return claimValue;
        }

        throw new InvalidOperationException($"Nenhuma claim foi encontrada para os tipos: {string.Join(", ", claimTypes)}.");
    }

}
