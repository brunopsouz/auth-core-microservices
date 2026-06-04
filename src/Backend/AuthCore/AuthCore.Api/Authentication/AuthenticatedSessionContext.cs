using System.Security.Claims;
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


    /// <inheritdoc />
    public Guid UserIdentifier
    {
        get
        {
            if (TryGetUserIdentifier(out var userIdentifier))
                return userIdentifier;

            throw new UnauthorizedAccessException("O identificador do usuario autenticado nao foi encontrado.");
        }
    }

    /// <inheritdoc />
    public Guid InternalUserId
    {
        get
        {
            var claimValue = _user.FindFirstValue(SessionAuthenticationDefaults.InternalUserIdClaimType);

            if (Guid.TryParse(claimValue, out var userId))
                return userId;

            throw new UnauthorizedAccessException("O identificador interno do usuario autenticado nao foi encontrado.");
        }
    }

    /// <inheritdoc />
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

            throw new UnauthorizedAccessException("O e-mail do usuario autenticado nao foi encontrado.");
        }
    }

    /// <inheritdoc />
    public string SessionId
    {
        get
        {
            var sessionId = _user.FindFirstValue(SessionAuthenticationDefaults.SessionIdClaimType)
                ?? _user.FindFirstValue("sid");

            if (!string.IsNullOrWhiteSpace(sessionId))
                return sessionId;

            throw new UnauthorizedAccessException("O identificador da sessao autenticada nao foi encontrado.");
        }
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public UserStatus UserStatus
    {
        get
        {
            var claimValue = _user.FindFirstValue(SessionAuthenticationDefaults.UserStatusClaimType);

            if (Enum.TryParse<UserStatus>(claimValue, out var userStatus))
                return userStatus;

            throw new UnauthorizedAccessException("O status do usuario autenticado nao foi encontrado.");
        }
    }

    /// <inheritdoc />
    public bool IsActive
    {
        get
        {
            var claimValue = _user.FindFirstValue(SessionAuthenticationDefaults.UserIsActiveClaimType);

            if (bool.TryParse(claimValue, out var userIsActive))
                return userIsActive;

            throw new UnauthorizedAccessException("O estado de atividade do usuario autenticado nao foi encontrado.");
        }
    }


    /// <inheritdoc />
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
