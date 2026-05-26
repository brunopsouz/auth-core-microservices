using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.Users.UseCases.GetUserProfile;

/// <summary>
/// Representa caso de uso para obter o perfil do usuário autenticado.
/// </summary>
internal sealed class GetUserProfileUseCase : IGetUserProfileUseCase
{
    /// <summary>
    /// Campo que armazena user read repository.
    /// </summary>
    private readonly IUserReadRepository _userReadRepository;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    public GetUserProfileUseCase(IUserReadRepository userReadRepository)
    {
        _userReadRepository = userReadRepository;
    }


    /// <summary>
    /// Operação para obter o perfil do usuário autenticado.
    /// </summary>
    /// <param name="query">Consulta do perfil do usuário.</param>
    /// <returns>Resultado com os dados do perfil do usuário.</returns>
    public async Task<GetUserProfileResult> Execute(GetUserProfileQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var user = await _userReadRepository.GetByUserIdentifierAsync(query.UserIdentifier);

        if (user is null || !user.IsActive)
            throw new NotFoundException("Usuário não encontrado.");

        return new GetUserProfileResult
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Email = user.Email.Value,
            Contact = user.Contact,
            Role = user.Role.ToString(),
            IsEmailVerified = user.IsEmailVerified
        };
    }
}
