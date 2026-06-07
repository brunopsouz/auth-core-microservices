namespace Gateway.Api.Authentication;

/// <summary>
/// Representa origem do access token usado na requisicao atual.
/// </summary>
internal static class AccessTokenSource
{
    /// <summary>
    /// Token recebido pelo header Authorization.
    /// </summary>
    public const string AuthorizationHeader = "AuthorizationHeader";

    /// <summary>
    /// Token recebido pelo cookie HttpOnly.
    /// </summary>
    public const string Cookie = "Cookie";
}
