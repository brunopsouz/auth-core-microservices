using System.Security.Claims;
using System.Text;
using AuthCore.Api.Authentication;
using AuthCore.Api.Exceptions;
using AuthCore.Api.HealthChecks;
using AuthCore.Api.Security;
using AuthCore.Api.Workers;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Net;

namespace AuthCore.Api;

/// <summary>
/// Define operações para registrar dependências da API.
/// </summary>
public static class ApiDependencyInjection
{
    private const string JwtAuthenticationScheme = JwtBearerDefaults.AuthenticationScheme;
    private const string PolicyAuthenticationScheme = "AuthCore";

    /// <summary>
    /// Operação para adicionar os serviços da API.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>Coleção de serviços atualizada.</returns>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddControllers();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddHttpContextAccessor();
        AddForwardedHeaders(services, configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILoginRateLimiter, RedisLoginRateLimiter>();
        services.AddScoped<ICsrfRequestValidator, CookieCsrfRequestValidator>();
        services.AddScoped<IAuthenticatedUserAccessValidator, AuthenticatedUserAccessValidator>();
        services.AddRouting(options => options.LowercaseUrls = true);
        services.AddAuthorization();
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("postgresql")
            .AddCheck<RedisHealthCheck>("redis")
            .AddCheck<OutboxHealthCheck>("outbox");
        services.AddHostedService<OutboxHostedService>();

        AddAuthentication(services, configuration);
        AddSwagger(services);

        return services;
    }


    /// <summary>
    /// Operação para adicionar a autenticação da API.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = GetJwtOptions(configuration);
        var authCookieOptions = GetAuthCookieOptions(configuration);

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = PolicyAuthenticationScheme;
                options.DefaultAuthenticateScheme = PolicyAuthenticationScheme;
                options.DefaultChallengeScheme = PolicyAuthenticationScheme;
            })
            .AddPolicyScheme(PolicyAuthenticationScheme, PolicyAuthenticationScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authorizationHeader = context.Request.Headers.Authorization.ToString();

                    if (!string.IsNullOrWhiteSpace(authorizationHeader)
                        && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return JwtAuthenticationScheme;
                    }

                    if (context.Request.Cookies.ContainsKey(authCookieOptions.SessionCookieName))
                        return SessionAuthenticationDefaults.AuthenticationScheme;

                    return JwtAuthenticationScheme;
                };
            })
            .AddJwtBearer(JwtAuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
            })
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                SessionAuthenticationDefaults.AuthenticationScheme,
                _ => { });
    }

    /// <summary>
    /// Operação para adicionar o tratamento de headers encaminhados por proxy.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddForwardedHeaders(IServiceCollection services, IConfiguration configuration)
    {
        var proxyForwardingOptions = configuration
            .GetSection(ProxyForwardingOptions.SectionName)
            .Get<ProxyForwardingOptions>()
            ?? new ProxyForwardingOptions();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedHost |
                ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = proxyForwardingOptions.ForwardLimit;

            foreach (var knownProxy in proxyForwardingOptions.KnownProxies)
                options.KnownProxies.Add(ParseIpAddress(knownProxy));

            foreach (var knownNetwork in proxyForwardingOptions.KnownNetworks)
                options.KnownNetworks.Add(ParseNetwork(knownNetwork));
        });
    }

    /// <summary>
    /// Operação para obter as configurações de JWT.
    /// </summary>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>Configurações válidas de JWT.</returns>
    private static JwtOptions GetJwtOptions(IConfiguration configuration)
    {
        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("As configurações de JWT não foram encontradas.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
            throw new InvalidOperationException("O emissor do JWT não foi configurado.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
            throw new InvalidOperationException("A audiência do JWT não foi configurada.");

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
            throw new InvalidOperationException("A chave de assinatura do JWT não foi configurada.");

        if (jwtOptions.AccessTokenLifetimeMinutes <= 0)
            throw new InvalidOperationException("O tempo de vida do access token deve ser maior que zero.");

        if (jwtOptions.RefreshTokenLifetimeDays <= 0)
            throw new InvalidOperationException("O tempo de vida do refresh token deve ser maior que zero.");

        if (jwtOptions.ClockSkewSeconds < 0)
            throw new InvalidOperationException("A tolerância de clock do JWT não pode ser negativa.");

        return jwtOptions;
    }

    /// <summary>
    /// Operação para obter as configurações do cookie de autenticação.
    /// </summary>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>Configurações válidas do cookie de autenticação.</returns>
    private static AuthCookieOptions GetAuthCookieOptions(IConfiguration configuration)
    {
        var authCookieOptions = configuration
            .GetSection(AuthCookieOptions.SectionName)
            .Get<AuthCookieOptions>()
            ?? new AuthCookieOptions();

        if (string.IsNullOrWhiteSpace(authCookieOptions.SessionCookieName))
            throw new InvalidOperationException("O nome do cookie da sessão não foi configurado.");

        return authCookieOptions;
    }

    /// <summary>
    /// Operação para adicionar a configuração do Swagger.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    private static void AddSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            var documentationFileName = $"{typeof(ApiDependencyInjection).Assembly.GetName().Name}.xml";
            var documentationFilePath = Path.Combine(AppContext.BaseDirectory, documentationFileName);

            options.IncludeXmlComments(documentationFilePath, includeControllerXmlComments: true);
            options.AddSecurityDefinition(JwtAuthenticationScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Informe o token JWT no formato Bearer."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = JwtAuthenticationScheme
                        }
                    },
                    []
                }
            });
        });
    }

    /// <summary>
    /// Operação para converter um endereço IP configurado.
    /// </summary>
    /// <param name="ipAddress">Endereço IP informado na configuração.</param>
    /// <returns>Endereço IP válido.</returns>
    private static IPAddress ParseIpAddress(string ipAddress)
    {
        if (IPAddress.TryParse(ipAddress?.Trim(), out var parsedIpAddress))
            return parsedIpAddress;

        throw new InvalidOperationException($"O proxy confiável '{ipAddress}' não possui um endereço IP válido.");
    }

    /// <summary>
    /// Operação para converter uma rede CIDR configurada.
    /// </summary>
    /// <param name="network">Rede informada na configuração.</param>
    /// <returns>Rede válida para confiança dos headers encaminhados.</returns>
    private static Microsoft.AspNetCore.HttpOverrides.IPNetwork ParseNetwork(string network)
    {
        var segments = (network ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length != 2
            || !IPAddress.TryParse(segments[0], out var prefix)
            || !int.TryParse(segments[1], out var prefixLength))
        {
            throw new InvalidOperationException($"A rede confiável '{network}' não está em formato CIDR válido.");
        }

        return new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength);
    }

}
