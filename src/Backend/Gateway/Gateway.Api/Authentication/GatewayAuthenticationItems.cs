namespace Gateway.Api.Authentication;

/// <summary>
/// Representa chaves usadas no contexto HTTP de autenticacao do Gateway.
/// </summary>
internal static class GatewayAuthenticationItems
{
    /// <summary>
    /// Chave que armazena a origem do access token autenticado.
    /// </summary>
    public const string AccessTokenSource = "Gateway.AccessTokenSource";
}
