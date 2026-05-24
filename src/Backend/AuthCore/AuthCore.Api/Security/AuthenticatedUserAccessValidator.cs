using System.Security.Claims;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa validador do acesso do usuário autenticado por bearer.
/// </summary>
internal sealed class AuthenticatedUserAccessValidator : IAuthenticatedUserAccessValidator
{
    private readonly IUserReadRepository _userReadRepository;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userReadRepository">Repositório de leitura do usuário autenticado.</param>
    public AuthenticatedUserAccessValidator(IUserReadRepository userReadRepository)
    {
        ArgumentNullException.ThrowIfNull(userReadRepository);

        _userReadRepository = userReadRepository;
    }

    #endregion

    /// <summary>
    /// Operação para validar o acesso do usuário autenticado e retornar seu identificador público atual.
    /// </summary>
    /// <param name="user">Principal autenticado da requisição atual.</param>
    /// <returns>Identificador público do usuário revalidado.</returns>
    public async Task<Guid> ValidateAndGetUserIdentifierAsync(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userIdentifier = GetAuthenticatedUserIdentifier(user);
        var authenticatedUser = await _userReadRepository.GetByUserIdentifierAsync(userIdentifier);

        if (authenticatedUser is null)
            throw new UnauthorizedAccessException("O usuário autenticado não está disponível.");

        if (!authenticatedUser.IsActive)
            throw new ForbiddenException("O usuário não pode autenticar no momento.");

        if (authenticatedUser.Status == UserStatus.PendingEmailVerification)
            throw new ForbiddenException("O usuário precisa verificar o e-mail antes de autenticar.");

        if (authenticatedUser.Status == UserStatus.Blocked)
            throw new ForbiddenException("O usuário está bloqueado para autenticação.");

        return authenticatedUser.UserIdentifier;
    }

    #region Helpers

    /// <summary>
    /// Operação para obter o identificador público do usuário autenticado.
    /// </summary>
    /// <param name="user">Principal autenticado da requisição atual.</param>
    /// <returns>Identificador público do usuário autenticado.</returns>
    private static Guid GetAuthenticatedUserIdentifier(ClaimsPrincipal user)
    {
        var claimValues = new[]
        {
            user.FindFirstValue(ClaimTypes.NameIdentifier),
            user.FindFirstValue("sub"),
            user.FindFirstValue("user_identifier"),
            user.FindFirstValue("userIdentifier")
        };

        foreach (var claimValue in claimValues)
        {
            if (Guid.TryParse(claimValue, out var userIdentifier))
                return userIdentifier;
        }

        throw new UnauthorizedAccessException("O identificador do usuário autenticado não foi encontrado.");
    }

    #endregion
}
