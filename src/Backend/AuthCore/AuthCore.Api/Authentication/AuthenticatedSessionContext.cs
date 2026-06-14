using System.Security.Claims;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Users;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa contexto da sessao autenticada.
/// </summary>
public sealed class AuthenticatedSessionContext : IAuthenticatedSessionContext
{
    /// <summary>
    /// Campo que armazena principal autenticado.
    /// </summary>
    private readonly ClaimsPrincipal _user;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="user">Principal autenticado.</param>
    public AuthenticatedSessionContext(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        _user = user;
    }


    /// <summary>
    /// Identificador publico do usuario autenticado.
    /// </summary>
    public Guid UserIdentifier
    {
        get
        {
            if (TryGetUserIdentifier(out var userIdentifier))
                return userIdentifier;

            throw new UnauthorizedException("O identificador do usuario autenticado nao foi encontrado.");
        }
    }

    /// <summary>
    /// Identificador interno do usuario autenticado.
    /// </summary>
    public Guid InternalUserId
    {
        get
        {
            var claimValue = _user.FindFirstValue(SessionAuthenticationDefaults.InternalUserIdClaimType);

            if (Guid.TryParse(claimValue, out var userId))
                return userId;

            throw new UnauthorizedException("O identificador interno do usuario autenticado nao foi encontrado.");
        }
    }

    /// <summary>
    /// E-mail do usuario autenticado.
    /// </summary>
    public string Email
    {
        get
        {
            var claimValues = new[]
            {
                _user.FindFirstValue(ClaimTypes.Email),
                _user.FindFirstValue("email")
            };

            foreach (var claimValue in claimValues)
            {
                if (!string.IsNullOrWhiteSpace(claimValue))
                    return claimValue;
            }

            throw new UnauthorizedException("O e-mail do usuario autenticado nao foi encontrado.");
        }
    }

    /// <summary>
    /// Identificador opaco da sessao autenticada.
    /// </summary>
    public string SessionId
    {
        get
        {
            var sessionId = _user.FindFirstValue(SessionAuthenticationDefaults.SessionIdClaimType)
                ?? _user.FindFirstValue("sid");

            if (!string.IsNullOrWhiteSpace(sessionId))
                return sessionId;

            throw new UnauthorizedException("O identificador da sessao autenticada nao foi encontrado.");
        }
    }

    /// <summary>
    /// Identificador publico da sessao autenticada.
    /// </summary>
    public string PublicSessionId
    {
        get
        {
            var publicSessionId = _user.FindFirstValue(SessionAuthenticationDefaults.PublicSessionIdClaimType);

            if (!string.IsNullOrWhiteSpace(publicSessionId))
                return publicSessionId;

            return SessionId;
        }
    }

    /// <summary>
    /// Status funcional do usuario autenticado.
    /// </summary>
    public UserStatus UserStatus
    {
        get
        {
            var claimValue = _user.FindFirstValue(SessionAuthenticationDefaults.UserStatusClaimType);

            if (Enum.TryParse<UserStatus>(claimValue, out var userStatus))
                return userStatus;

            throw new UnauthorizedException("O status do usuario autenticado nao foi encontrado.");
        }
    }

    /// <summary>
    /// Indicacao de atividade do usuario autenticado.
    /// </summary>
    public bool IsActive
    {
        get
        {
            var claimValue = _user.FindFirstValue(SessionAuthenticationDefaults.UserIsActiveClaimType);

            if (bool.TryParse(claimValue, out var userIsActive))
                return userIsActive;

            throw new UnauthorizedException("O estado de atividade do usuario autenticado nao foi encontrado.");
        }
    }


    /// <summary>
    /// Operacao para tentar obter o identificador publico do usuario autenticado.
    /// </summary>
    /// <param name="userIdentifier">Identificador publico do usuario autenticado.</param>
    /// <returns>Verdadeiro quando o identificador foi encontrado.</returns>
    public bool TryGetUserIdentifier(out Guid userIdentifier)
    {
        var claimValues = new[]
        {
            _user.FindFirstValue(ClaimTypes.NameIdentifier),
            _user.FindFirstValue("sub"),
            _user.FindFirstValue("user_identifier"),
            _user.FindFirstValue("userIdentifier")
        };

        foreach (var claimValue in claimValues)
        {
            if (Guid.TryParse(claimValue, out userIdentifier))
                return true;
        }

        userIdentifier = Guid.Empty;
        return false;
    }
}
