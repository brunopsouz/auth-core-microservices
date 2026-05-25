using AuthCore.Infrastructure.Configurations;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Define operações para padronizar a política do cookie de sessão.
/// </summary>
internal static class SessionCookiePolicy
{
    /// <summary>
    /// Operação para criar as opções de emissão do cookie da sessão.
    /// </summary>
    /// <param name="authCookieOptions">Configurações do cookie de autenticação.</param>
    /// <param name="expiresAtUtc">Data de expiração da sessão em UTC.</param>
    /// <returns>Configuração padronizada do cookie de sessão.</returns>
    public static CookieOptions CreateSessionCookie(
        AuthCookieOptions authCookieOptions,
        DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(authCookieOptions);

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = authCookieOptions.Secure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = new DateTimeOffset(expiresAtUtc)
        };
    }

    /// <summary>
    /// Operação para criar as opções de remoção do cookie da sessão.
    /// </summary>
    /// <param name="authCookieOptions">Configurações do cookie de autenticação.</param>
    /// <returns>Configuração padronizada de remoção do cookie de sessão.</returns>
    public static CookieOptions CreateExpiredSessionCookie(AuthCookieOptions authCookieOptions)
    {
        ArgumentNullException.ThrowIfNull(authCookieOptions);

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = authCookieOptions.Secure,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };
    }
}
