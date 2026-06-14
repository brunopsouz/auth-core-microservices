using System.Net;
using System.Security.Claims;
using AuthCore.Api.Authentication;
using AuthCore.Api.Exceptions;
using AuthCore.Api.HealthChecks;
using AuthCore.Api.Security;
using AuthCore.Api.Workers;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace AuthCore.Api;

/// <summary>
/// Define operacoes para registrar dependencias da API.
/// </summary>
public static class ApiDependencyInjection
{
    private const string BrowserSessionCorsPolicyName = "AuthCoreBrowserSession";
    private const string JwtAuthenticationScheme = JwtBearerDefaults.AuthenticationScheme;
    private const string PolicyAuthenticationScheme = "AuthCore";

    /// <summary>
    /// Operacao para adicionar os servicos da API.
    /// </summary>
    /// <param name="services">Colecao de servicos da aplicacao.</param>
    /// <param name="configuration">Configuracao da aplicacao.</param>
    /// <returns>Colecao de servicos atualizada.</returns>
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
        AddCors(services, configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILoginRateLimiter, RedisLoginRateLimiter>();
        services.AddScoped<ICsrfTokenService, CookieCsrfTokenService>();
        services.AddScoped<ICsrfRequestValidator, CookieCsrfRequestValidator>();
        services.AddScoped<IAuthenticatedUserAccessValidator, AuthenticatedUserAccessValidator>();
        services.AddScoped<IAuthenticatedSessionContext>(serviceProvider =>
        {
            var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

            return new AuthenticatedSessionContext(httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal());
        });
        services.AddScoped<IAuthorizationHandler, ActiveSessionAuthorizationHandler>();
        services.AddRouting(options => options.LowercaseUrls = true);
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ActiveSession", policy =>
            {
                policy.AuthenticationSchemes.Add(SessionAuthenticationDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new ActiveSessionRequirement());
            });
        });
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
    /// Operacao para adicionar a politica CORS das rotas browser/session.
    /// </summary>
    /// <param name="services">Colecao de servicos da aplicacao.</param>
    /// <param name="configuration">Configuracao da aplicacao.</param>
    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var csrfOptions = ApiSecurityOptions.GetCsrfOptions(configuration);
        var allowedOrigins = csrfOptions.AllowedOrigins
            .Select(ApiSecurityOptions.NormalizeOrigin)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        services.AddCors(options =>
        {
            options.AddPolicy(BrowserSessionCorsPolicyName, policy =>
            {
                if (allowedOrigins.Length == 0)
                    return;

                policy
                    .WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .WithHeaders("Content-Type", "Authorization", csrfOptions.HeaderName)
                    .AllowCredentials();
            });
        });
    }


    /// <summary>
    /// Operacao para adicionar a autenticacao da API.
    /// </summary>
    /// <param name="services">Colecao de servicos da aplicacao.</param>
    /// <param name="configuration">Configuracao da aplicacao.</param>
    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = ApiSecurityOptions.GetJwtOptions(configuration);
        var authCookieOptions = ApiSecurityOptions.GetAuthCookieOptions(configuration);

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
                    IssuerSigningKey = new SymmetricSecurityKey(ApiSecurityOptions.ResolveSigningKeyBytes(jwtOptions.SigningKey)),
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
    /// Operacao para adicionar o tratamento de headers encaminhados por proxy.
    /// </summary>
    /// <param name="services">Colecao de servicos da aplicacao.</param>
    /// <param name="configuration">Configuracao da aplicacao.</param>
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
                options.KnownIPNetworks.Add(ParseNetwork(knownNetwork));
        });
    }

    /// <summary>
    /// Operacao para adicionar a configuracao do Swagger.
    /// </summary>
    /// <param name="services">Colecao de servicos da aplicacao.</param>
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

            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference(JwtAuthenticationScheme, null, null),
                    []
                }
            });
        });
    }

    /// <summary>
    /// Operacao para converter um endereco IP configurado.
    /// </summary>
    /// <param name="ipAddress">Endereco IP informado na configuracao.</param>
    /// <returns>Endereco IP valido.</returns>
    private static IPAddress ParseIpAddress(string ipAddress)
    {
        if (IPAddress.TryParse(ipAddress?.Trim(), out var parsedIpAddress))
            return parsedIpAddress;

        throw new InvalidOperationException($"O proxy confiavel '{ipAddress}' nao possui um endereco IP valido.");
    }

    /// <summary>
    /// Operacao para converter uma rede CIDR configurada.
    /// </summary>
    /// <param name="network">Rede informada na configuracao.</param>
    /// <returns>Rede valida para confianca dos headers encaminhados.</returns>
    private static System.Net.IPNetwork ParseNetwork(string network)
    {
        var segments = (network ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length != 2
            || !IPAddress.TryParse(segments[0], out var prefix)
            || !int.TryParse(segments[1], out var prefixLength))
        {
            throw new InvalidOperationException($"A rede confiavel '{network}' nao esta em formato CIDR valido.");
        }

        return new System.Net.IPNetwork(prefix, prefixLength);
    }

}
