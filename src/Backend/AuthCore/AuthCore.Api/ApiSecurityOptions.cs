using System.Text;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Configuration;

namespace AuthCore.Api;

/// <summary>
/// Representa operacoes de leitura e validacao das opcoes de seguranca da API.
/// </summary>
internal static class ApiSecurityOptions
{
    private const int MinimumHs256KeyBytes = 32;
    private const int MaximumAccessTokenLifetimeMinutes = 5;

    /// <summary>
    /// Operacao para obter as configuracoes de JWT.
    /// </summary>
    /// <param name="configuration">Configuracao da aplicacao.</param>
    /// <returns>Configuracoes validas de JWT.</returns>
    public static JwtOptions GetJwtOptions(IConfiguration configuration)
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
    public static AuthCookieOptions GetAuthCookieOptions(IConfiguration configuration)
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
    /// Operacao para obter as configuracoes da protecao CSRF.
    /// </summary>
    /// <param name="configuration">Configuracao da aplicacao.</param>
    /// <returns>Configuracoes validas da protecao CSRF.</returns>
    public static CsrfOptions GetCsrfOptions(IConfiguration configuration)
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
    /// Operacao para resolver os bytes da chave de assinatura do JWT.
    /// </summary>
    /// <param name="signingKey">Chave configurada.</param>
    /// <returns>Bytes normalizados da chave.</returns>
    public static byte[] ResolveSigningKeyBytes(string signingKey)
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
    /// Operacao para normalizar uma origem configurada para CORS/CSRF.
    /// </summary>
    /// <param name="origin">Origem configurada.</param>
    /// <returns>Origem normalizada.</returns>
    public static string NormalizeOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return string.Empty;

        if (string.Equals(origin.Trim(), "*", StringComparison.Ordinal))
            return "*";

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
            return string.Empty;

        return uri.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>
    /// Operacao para validar compatibilidade do prefixo __Host- com as configuracoes do cookie.
    /// </summary>
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
    /// Operacao para indicar se a chave textual bruta possui um padrao fraco.
    /// </summary>
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
