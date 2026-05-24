using AuthCore.Domain.Security.Tokens.Models;
using AuthCore.Domain.Users;

namespace AuthCore.Domain.Security.Tokens.Services;

/// <summary>
/// Define operação para gerar access token.
/// </summary>
public interface IAccessTokenGenerator
{
    /// <summary>
    /// Operação para gerar um access token para o usuário.
    /// </summary>
    /// <param name="user">Usuário autenticado.</param>
    /// <returns>Resultado da emissão do token.</returns>
    AccessTokenResult Generate(User user);
}
