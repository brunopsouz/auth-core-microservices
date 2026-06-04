using AuthCore.Domain.Security.Tokens;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Define constantes do esquema de autenticação por sessão.
/// </summary>
public static class SessionAuthenticationDefaults
{
    /// <summary>
    /// Nome do esquema de autenticação por sessão.
    /// </summary>
    public const string AuthenticationScheme = "Session";

    /// <summary>
    /// Tipo de claim com o identificador interno do usuário.
    /// </summary>
    public const string InternalUserIdClaimType = "authcore_user_id";

    /// <summary>
    /// Tipo de claim com o identificador público da sessão.
    /// </summary>
    public const string SessionIdClaimType = "sid";

    /// <summary>
    /// Tipo de claim com o identificador publico da sessao.
    /// </summary>
    public const string PublicSessionIdClaimType = "public_sid";

    /// <summary>
    /// Tipo de claim com o status funcional do usuário.
    /// </summary>
    public const string UserStatusClaimType = AuthTokenClaimTypes.UserStatus;

    /// <summary>
    /// Tipo de claim com o indicador de atividade do usuário.
    /// </summary>
    public const string UserIsActiveClaimType = AuthTokenClaimTypes.UserIsActive;
}
