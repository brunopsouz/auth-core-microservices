using System.Net;
using System.Text;
using Gateway.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using NetIPNetwork = System.Net.IPNetwork;

namespace Gateway.Api;

/// <summary>
/// Define operações para registrar dependências do Gateway.
/// </summary>
public static class GatewayDependencyInjection
{
    private const int MinimumHs256KeyBytes = 32;

    /// <summary>
    /// Operação para adicionar os serviços do Gateway.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>Coleção de serviços atualizada.</returns>
    public static IServiceCollection AddGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddJwtOptions(services, configuration);
        AddForwardedHeaders(services, configuration);
        AddAuthentication(services, configuration);

        services.AddAuthorization();
        services.AddHealthChecks();
        services.AddRouting(options => options.LowercaseUrls = true);
        services.AddOcelot(configuration);

        return services;
    }


    /// <summary>
    /// Operação para adicionar as opções de JWT.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddJwtOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
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
                options.KnownIPNetworks.Add(ParseNetwork(knownNetwork));
        });
    }

    /// <summary>
    /// Operação para adicionar autenticação JWT.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = GetJwtOptions(configuration);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(ResolveSigningKeyBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds)
                };
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

        if (jwtOptions.ClockSkewSeconds < 0)
            throw new InvalidOperationException("A tolerância de clock do JWT não pode ser negativa.");

        _ = ResolveSigningKeyBytes(jwtOptions.SigningKey);

        return jwtOptions;
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
    private static NetIPNetwork ParseNetwork(string network)
    {
        var segments = (network ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length != 2
            || !IPAddress.TryParse(segments[0], out var prefix)
            || !int.TryParse(segments[1], out var prefixLength))
        {
            throw new InvalidOperationException($"A rede confiável '{network}' não está em formato CIDR válido.");
        }

        return new NetIPNetwork(prefix, prefixLength);
    }

    /// <summary>
    /// OperaÃ§Ã£o para resolver os bytes da chave de assinatura do JWT.
    /// </summary>
    /// <param name="signingKey">Chave configurada.</param>
    /// <returns>Bytes normalizados da chave.</returns>
    private static byte[] ResolveSigningKeyBytes(string signingKey)
    {
        var normalizedSigningKey = string.IsNullOrWhiteSpace(signingKey)
            ? string.Empty
            : signingKey.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSigningKey))
            throw new InvalidOperationException("A chave de assinatura do JWT nÃ£o foi configurada.");

        var isBase64 = TryDecodeBase64(normalizedSigningKey, out var decodedBytes);
        var keyBytes = isBase64
            ? decodedBytes!
            : Encoding.UTF8.GetBytes(normalizedSigningKey);

        if (keyBytes.Length < MinimumHs256KeyBytes)
            throw new InvalidOperationException("A chave HS256 do JWT deve possuir no mÃ­nimo 256 bits.");

        if (IsWeakRawSigningKey(normalizedSigningKey, isBase64))
            throw new InvalidOperationException("A chave HS256 do JWT Ã© fraca e precisa ser substituÃ­da.");

        return keyBytes;
    }

    /// <summary>
    /// OperaÃ§Ã£o para indicar se a chave textual bruta possui um padrÃ£o fraco.
    /// </summary>
    /// <param name="normalizedSigningKey">Chave normalizada.</param>
    /// <param name="isBase64">Indica se a chave foi fornecida em Base64.</param>
    /// <returns><c>true</c> quando a chave textual Ã© fraca; caso contrÃ¡rio, <c>false</c>.</returns>
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
    /// OperaÃ§Ã£o para tentar decodificar a chave como Base64.
    /// </summary>
    /// <param name="signingKey">Chave configurada.</param>
    /// <param name="decodedBytes">Bytes decodificados quando a conversÃ£o for bem-sucedida.</param>
    /// <returns><c>true</c> quando a chave estava em Base64 vÃ¡lido; caso contrÃ¡rio, <c>false</c>.</returns>
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
    /// OperaÃ§Ã£o para contar grupos de caracteres distintos presentes na chave textual.
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

}
