using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthCore.Domain.Passports;
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
    private const int MinimumHs256KeyBytes = 32;
    private const int MaximumAccessTokenLifetimeMinutes = 5;

    /// <summary>
    /// Campo que armazena jwt options.
    /// </summary>
    private readonly JwtOptions _jwtOptions;
    /// <summary>
    /// Campo que armazena signing credentials.
    /// </summary>
    private readonly SigningCredentials _signingCredentials;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="jwtOptions">Configuracoes de emissao do JWT.</param>
    public JwtAccessTokenGenerator(IOptions<JwtOptions> jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);

        _jwtOptions = jwtOptions.Value;
        ValidateJwtOptions(_jwtOptions);
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(ResolveSigningKeyBytes(_jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }


    /// <summary>
    /// Operacao para gerar um access token para o usuario.
    /// </summary>
    /// <param name="user">Usuario autenticado.</param>
    /// <param name="session">Sessao autenticada associada ao token, quando houver.</param>
    /// <returns>Resultado da emissao do token.</returns>
    public AccessTokenResult Generate(User user, Session? session = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var tokenId = Guid.NewGuid();
        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes);
        var claims = CreateClaims(user, tokenId, session);
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


    /// <summary>
    /// Operacao para criar as claims do access token.
    /// </summary>
    /// <param name="user">Usuario autenticado.</param>
    /// <param name="tokenId">Identificador do token emitido.</param>
    /// <param name="session">Sessao autenticada associada ao token, quando houver.</param>
    /// <returns>Colecao de claims do token.</returns>
    private static IReadOnlyCollection<Claim> CreateClaims(User user, Guid tokenId, Session? session)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserIdentifier.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserIdentifier.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("role", user.Role.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(ClaimTypes.Email, user.Email.Value),
            new(AuthTokenClaimTypes.UserStatus, user.Status.ToString()),
            new(AuthTokenClaimTypes.UserIsActive, user.IsActive.ToString()),
            new(JwtRegisteredClaimNames.Jti, tokenId.ToString())
        };

        if (session is not null)
            claims.Add(new Claim(AuthTokenClaimTypes.SessionId, session.SessionId));

        return claims;
    }

    /// <summary>
    /// Operacao para validar as configuracoes do JWT.
    /// </summary>
    /// <param name="jwtOptions">Configuracoes do JWT.</param>
    private static void ValidateJwtOptions(JwtOptions jwtOptions)
    {
        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
            throw new InvalidOperationException("O emissor do JWT nao foi configurado.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
            throw new InvalidOperationException("A audiencia do JWT nao foi configurada.");

        if (jwtOptions.AccessTokenLifetimeMinutes <= 0 || jwtOptions.AccessTokenLifetimeMinutes > MaximumAccessTokenLifetimeMinutes)
        {
            throw new InvalidOperationException(
                $"O tempo de vida do access token deve ser um valor curto entre 1 e {MaximumAccessTokenLifetimeMinutes} minutos.");
        }

        _ = ResolveSigningKeyBytes(jwtOptions.SigningKey);
    }

    /// <summary>
    /// Operacao para resolver os bytes da chave de assinatura do JWT.
    /// </summary>
    /// <param name="signingKey">Chave configurada.</param>
    /// <returns>Bytes normalizados da chave.</returns>
    private static byte[] ResolveSigningKeyBytes(string signingKey)
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
    /// Operacao para indicar se a chave textual bruta possui um padrao fraco.
    /// </summary>
    /// <param name="normalizedSigningKey">Chave normalizada.</param>
    /// <param name="isBase64">Indica se a chave foi fornecida em Base64.</param>
    /// <returns><c>true</c> quando a chave textual e fraca; caso contrario, <c>false</c>.</returns>
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
    /// <param name="signingKey">Chave configurada.</param>
    /// <param name="decodedBytes">Bytes decodificados quando a conversao for bem-sucedida.</param>
    /// <returns><c>true</c> quando a chave estava em Base64 valido; caso contrario, <c>false</c>.</returns>
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
