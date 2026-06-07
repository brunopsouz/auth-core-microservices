using System.Net;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace AuthCore.Api;

/// <summary>
/// Define operacoes para registrar dependencias da API.
/// </summary>
public static class ApiDependencyInjection
{
    private const int MinimumHs256KeyBytes = 32;
    private const int MaximumAccessTokenLifetimeMinutes = 5;
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
        var csrfOptions = GetCsrfOptions(configuration);
        var allowedOrigins = csrfOptions.AllowedOrigins
            .Select(NormalizeOrigin)
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
                    IssuerSigningKey = new SymmetricSecurityKey(ResolveSigningKeyBytes(jwtOptions.SigningKey)),
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
    /// Operacao para obter as configuracoes de JWT.
    /// </summary>
    /// <param name="configuration">Configuracao da aplicacao.</param>
    /// <returns>Configuracoes validas de JWT.</returns>
    private static JwtOptions GetJwtOptions(IConfiguration configuration)
    {
        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("As configuracoes de JWT nao foram encontradas.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
            throw new InvalidOperationException("O emissor do JWT nao foi configurado.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
            throw new InvalidOperationException("A audiencia do JWT nao foi configurada.");

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
            throw new InvalidOperationException("A chave de assinatura do JWT nao foi configurada.");

        if (jwtOptions.AccessTokenLifetimeMinutes <= 0 || jwtOptions.AccessTokenLifetimeMinutes > MaximumAccessTokenLifetimeMinutes)
        {
            throw new InvalidOperationException(
                $"O tempo de vida do access token deve ser um valor curto entre 1 e {MaximumAccessTokenLifetimeMinutes} minutos.");
        }

        if (jwtOptions.RefreshTokenLifetimeDays <= 0)
            throw new InvalidOperationException("O tempo de vida do refresh token deve ser maior que zero.");

        if (jwtOptions.ClockSkewSeconds < 0)
            throw new InvalidOperationException("A tolerancia de clock do JWT nao pode ser negativa.");

        _ = ResolveSigningKeyBytes(jwtOptions.SigningKey);

        return jwtOptions;
    }

    /// <summary>
    /// Operacao para obter as configuracoes do cookie de autenticacao.
    /// </summary>
    /// <param name="configuration">Configuracao da aplicacao.</param>
    /// <returns>Configuracoes validas do cookie de autenticacao.</returns>
    private static AuthCookieOptions GetAuthCookieOptions(IConfiguration configuration)
    {
        var authCookieOptions = configuration
            .GetSection(AuthCookieOptions.SectionName)
            .Get<AuthCookieOptions>()
            ?? new AuthCookieOptions();

        if (string.IsNullOrWhiteSpace(authCookieOptions.SessionCookieName))
            throw new InvalidOperationException("O nome do cookie da sessao nao foi configurado.");

        if (string.IsNullOrWhiteSpace(authCookieOptions.AccessTokenCookieName))
            throw new InvalidOperationException("O nome do cookie do access token nao foi configurado.");

        if (string.IsNullOrWhiteSpace(authCookieOptions.Path))
            throw new InvalidOperationException("O caminho padrao dos cookies de autenticacao nao foi configurado.");

        ValidateHostCookieCompatibility(authCookieOptions.SessionCookieName, authCookieOptions);
        ValidateHostCookieCompatibility(authCookieOptions.AccessTokenCookieName, authCookieOptions);

        return authCookieOptions;
    }

    /// <summary>
    /// Operacao para validar compatibilidade do prefixo __Host- com as configuracoes do cookie.
    /// </summary>
    /// <param name="cookieName">Nome configurado do cookie.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    private static void ValidateHostCookieCompatibility(string cookieName, AuthCookieOptions authCookieOptions)
    {
        if (!cookieName.StartsWith("__Host-", StringComparison.Ordinal))
            return;

        if (!authCookieOptions.Secure)
            throw new InvalidOperationException($"O cookie '{cookieName}' precisa de Secure=true para usar o prefixo __Host-.");

        if (!string.Equals(authCookieOptions.Path.Trim(), "/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"O cookie '{cookieName}' precisa de Path='/' para usar o prefixo __Host-.");
        }

        if (!string.IsNullOrWhiteSpace(authCookieOptions.Domain))
        {
            throw new InvalidOperationException(
                $"O cookie '{cookieName}' nao pode definir Domain quando usa o prefixo __Host-.");
        }
    }

    /// <summary>
    /// Operacao para obter as configuracoes da protecao CSRF.
    /// </summary>
    /// <param name="configuration">Configuracao da aplicacao.</param>
    /// <returns>Configuracoes validas da protecao CSRF.</returns>
    private static CsrfOptions GetCsrfOptions(IConfiguration configuration)
    {
        var csrfOptions = configuration
            .GetSection(CsrfOptions.SectionName)
            .Get<CsrfOptions>()
            ?? new CsrfOptions();

        if (string.IsNullOrWhiteSpace(csrfOptions.HeaderName))
            throw new InvalidOperationException("O nome do header CSRF nao foi configurado.");

        if (string.IsNullOrWhiteSpace(csrfOptions.CookieName))
            throw new InvalidOperationException("O nome do cookie CSRF nao foi configurado.");

        if (string.IsNullOrWhiteSpace(csrfOptions.SigningKey))
            throw new InvalidOperationException("A chave de assinatura do CSRF nao foi configurada.");

        foreach (var allowedOrigin in csrfOptions.AllowedOrigins)
        {
            var normalizedOrigin = NormalizeOrigin(allowedOrigin);

            if (string.IsNullOrWhiteSpace(normalizedOrigin))
                throw new InvalidOperationException($"A origem CORS/CSRF '{allowedOrigin}' nao e valida.");

            if (string.Equals(normalizedOrigin, "*", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Nao e permitido usar '*' nas origens CORS quando a autenticacao por cookie exige credentials.");
            }
        }

        return csrfOptions;
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

    /// <summary>
    /// Operacao para resolver os bytes da chave de assinatura do JWT.
    /// </summary>
    /// <param name="signingKey">Chave configurada.</param>
    /// <returns>Bytes normalizados da chave.</returns>
    private static byte[] ResolveSigningKeyBytes(string signingKey)
    {
        var normalizedSigningKey = string.IsNullOrWhiteSpace(signingKey)
            ? string.Empty
            : signingKey.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSigningKey))
            throw new InvalidOperationException("A chave de assinatura do JWT nao foi configurada.");

        var isBase64 = TryDecodeBase64(normalizedSigningKey, out var decodedBytes);
        var keyBytes = isBase64
            ? decodedBytes!
            : Encoding.UTF8.GetBytes(normalizedSigningKey);

        if (keyBytes.Length < MinimumHs256KeyBytes)
            throw new InvalidOperationException("A chave HS256 do JWT deve possuir no minimo 256 bits.");

        if (IsWeakRawSigningKey(normalizedSigningKey, isBase64))
            throw new InvalidOperationException("A chave HS256 do JWT e fraca e precisa ser substituida.");

        return keyBytes;
    }

    /// <summary>
    /// Operacao para indicar se a chave textual bruta possui um padrao fraco.
    /// </summary>
    /// <param name="normalizedSigningKey">Chave normalizada.</param>
    /// <param name="isBase64">Indica se a chave foi fornecida em Base64.</param>
    /// <returns><c>true</c> quando a chave textual e fraca; caso contrario, <c>false</c>.</returns>
    private static bool IsWeakRawSigningKey(string normalizedSigningKey, bool isBase64)
    {
        if (isBase64)
            return false;

        var distinctCharacters = normalizedSigningKey
            .Distinct()
            .Count();
        var categories = CountCharacterCategories(normalizedSigningKey);

        return distinctCharacters < 8
            || categories < 3
            || normalizedSigningKey.All(character => character == normalizedSigningKey[0]);
    }

    /// <summary>
    /// Operacao para tentar decodificar a chave como Base64.
    /// </summary>
    /// <param name="signingKey">Chave configurada.</param>
    /// <param name="decodedBytes">Bytes decodificados quando a conversao for bem-sucedida.</param>
    /// <returns><c>true</c> quando a chave estava em Base64 valido; caso contrario, <c>false</c>.</returns>
    private static bool TryDecodeBase64(string signingKey, out byte[]? decodedBytes)
    {
        try
        {
            decodedBytes = Convert.FromBase64String(signingKey);
            return true;
        }
        catch (FormatException)
        {
            decodedBytes = null;
            return false;
        }
    }

    /// <summary>
    /// Operacao para contar grupos de caracteres distintos presentes na chave textual.
    /// </summary>
    /// <param name="signingKey">Chave textual informada.</param>
    /// <returns>Total de grupos de caracteres presentes.</returns>
    private static int CountCharacterCategories(string signingKey)
    {
        var categories = 0;

        if (signingKey.Any(char.IsLower))
            categories++;

        if (signingKey.Any(char.IsUpper))
            categories++;

        if (signingKey.Any(char.IsDigit))
            categories++;

        if (signingKey.Any(character => !char.IsLetterOrDigit(character)))
            categories++;

        return categories;
    }

    /// <summary>
    /// Operacao para normalizar uma origem configurada para CORS/CSRF.
    /// </summary>
    /// <param name="origin">Origem configurada.</param>
    /// <returns>Origem normalizada.</returns>
    private static string NormalizeOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return string.Empty;

        if (string.Equals(origin.Trim(), "*", StringComparison.Ordinal))
            return "*";

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
            return string.Empty;

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
