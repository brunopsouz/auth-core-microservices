using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthCore.Domain.Security.Tokens;
using AuthCore.Domain.Security.Tokens.Models;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthCore.Infrastructure.Security.Tokens;

/// <summary>
/// Representa gerador JWT de access token.
/// </summary>
internal sealed class JwtAccessTokenGenerator : IAccessTokenGenerator
{
    private readonly JwtOptions _jwtOptions;
    private readonly SigningCredentials _signingCredentials;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="jwtOptions">Configurações de emissão do JWT.</param>
    public JwtAccessTokenGenerator(IOptions<JwtOptions> jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);

        _jwtOptions = jwtOptions.Value;
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    #endregion

    /// <summary>
    /// Operação para gerar um access token para o usuário.
    /// </summary>
    /// <param name="user">Usuário autenticado.</param>
    /// <returns>Resultado da emissão do token.</returns>
    public AccessTokenResult Generate(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var tokenId = Guid.NewGuid();
        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes);
        var claims = CreateClaims(user, tokenId);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = issuedAtUtc,
            IssuedAt = issuedAtUtc,
            Expires = expiresAtUtc,
            SigningCredentials = _signingCredentials
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);

        return new AccessTokenResult
        {
            Token = tokenHandler.WriteToken(securityToken),
            TokenId = tokenId,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    #region Helpers

    /// <summary>
    /// Operação para criar as claims do access token.
    /// </summary>
    /// <param name="user">Usuário autenticado.</param>
    /// <param name="tokenId">Identificador do token emitido.</param>
    /// <returns>Coleção de claims do token.</returns>
    private static IReadOnlyCollection<Claim> CreateClaims(User user, Guid tokenId)
    {
        return
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.UserIdentifier.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.UserIdentifier.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
            new Claim(ClaimTypes.Email, user.Email.Value),
            new Claim(AuthTokenClaimTypes.UserStatus, user.Status.ToString()),
            new Claim(AuthTokenClaimTypes.UserIsActive, user.IsActive.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, tokenId.ToString())
        ];
    }

    #endregion
}
