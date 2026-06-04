using AuthCore.Infrastructure.Configurations;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Define operacoes para padronizar a politica dos cookies de autenticacao.
/// </summary>
internal static class SessionCookiePolicy
{
    /// <summary>
    /// Operacao para criar as opcoes de emissao do cookie da sessao.
    /// </summary>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="expiresAtUtc">Data de expiracao da sessao em UTC.</param>
    /// <returns>Configuracao padronizada do cookie de sessao.</returns>
    public static CookieOptions CreateSessionCookie(
        AuthCookieOptions authCookieOptions,
        DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(authCookieOptions);

        return CreateCookieOptions(authCookieOptions, expiresAtUtc, authCookieOptions.HttpOnly);
    }

    /// <summary>
    /// Operacao para criar as opcoes de emissao do cookie do access token.
    /// </summary>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="expiresAtUtc">Data de expiracao do access token em UTC.</param>
    /// <returns>Configuracao padronizada do cookie do access token.</returns>
    public static CookieOptions CreateAccessTokenCookie(
        AuthCookieOptions authCookieOptions,
        DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(authCookieOptions);

        return CreateCookieOptions(authCookieOptions, expiresAtUtc, authCookieOptions.HttpOnly);
    }

    /// <summary>
    /// Operacao para criar as opcoes de emissao do cookie do token CSRF.
    /// </summary>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="expiresAtUtc">Data de expiracao da sessao em UTC.</param>
    /// <returns>Configuracao padronizada do cookie do token CSRF.</returns>
    public static CookieOptions CreateCsrfCookie(
        AuthCookieOptions authCookieOptions,
        DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(authCookieOptions);

        return CreateCookieOptions(authCookieOptions, expiresAtUtc, httpOnly: false);
    }

    /// <summary>
    /// Operacao para criar as opcoes de remocao dos cookies de autenticacao.
    /// </summary>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="httpOnly">Indica se o cookie expirado deve ser HttpOnly.</param>
    /// <returns>Configuracao padronizada de remocao do cookie.</returns>
    public static CookieOptions CreateExpiredCookie(AuthCookieOptions authCookieOptions, bool httpOnly)
    {
        ArgumentNullException.ThrowIfNull(authCookieOptions);

        return CreateCookieOptions(authCookieOptions, expiresAtUtc: null, httpOnly);
    }


    /// <summary>
    /// Operacao para criar as opcoes padronizadas dos cookies de autenticacao.
    /// </summary>
    /// <param name="authCookieOptions">Configuracoes do cookie de autenticacao.</param>
    /// <param name="expiresAtUtc">Data de expiracao do cookie em UTC quando aplicavel.</param>
    /// <param name="httpOnly">Indica se o cookie deve ser HttpOnly.</param>
    /// <returns>Configuracao padronizada do cookie.</returns>
    private static CookieOptions CreateCookieOptions(
        AuthCookieOptions authCookieOptions,
        DateTime? expiresAtUtc,
        bool httpOnly)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = authCookieOptions.Secure,
            SameSite = ResolveSameSite(authCookieOptions.SameSite),
            Path = authCookieOptions.Path
        };

        if (!string.IsNullOrWhiteSpace(authCookieOptions.Domain))
            cookieOptions.Domain = authCookieOptions.Domain.Trim();

        if (expiresAtUtc.HasValue)
            cookieOptions.Expires = new DateTimeOffset(expiresAtUtc.Value);

        return cookieOptions;
    }

    /// <summary>
    /// Operacao para resolver a politica SameSite configurada.
    /// </summary>
    /// <param name="sameSite">Valor configurado.</param>
    /// <returns>Politica SameSite resolvida.</returns>
    private static SameSiteMode ResolveSameSite(string sameSite)
    {
        return Enum.TryParse<SameSiteMode>(sameSite?.Trim(), ignoreCase: true, out var resolvedSameSite)
            ? resolvedSameSite
            : SameSiteMode.Lax;
    }
}
