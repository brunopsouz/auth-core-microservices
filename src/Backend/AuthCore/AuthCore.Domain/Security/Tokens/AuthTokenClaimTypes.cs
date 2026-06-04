namespace AuthCore.Domain.Security.Tokens;

/// <summary>
/// Define os tipos de claims compartilhados pela autenticação baseada em token.
/// </summary>
public static class AuthTokenClaimTypes
{
    /// <summary>
    /// Tipo de claim com o identificador opaco da sessao autenticada.
    /// </summary>
    public const string SessionId = "sid";

    /// <summary>
    /// Tipo de claim com o status funcional do usuário.
    /// </summary>
    public const string UserStatus = "authcore_user_status";

    /// <summary>
    /// Tipo de claim com o indicador de atividade do usuário.
    /// </summary>
    public const string UserIsActive = "authcore_user_is_active";
}
