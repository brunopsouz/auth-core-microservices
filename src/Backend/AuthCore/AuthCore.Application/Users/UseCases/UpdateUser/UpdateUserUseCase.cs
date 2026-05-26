using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.Users.UseCases.UpdateUser;

/// <summary>
/// Representa caso de uso para atualizar o perfil do usuário autenticado.
/// </summary>
internal sealed class UpdateUserUseCase : IUpdateUserUseCase
{
    /// <summary>
    /// Campo que armazena user read repository.
    /// </summary>
    private readonly IUserReadRepository _userReadRepository;
    /// <summary>
    /// Campo que armazena user repository.
    /// </summary>
    private readonly IUserRepository _userRepository;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userRepository">Repositório de escrita de usuário.</param>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    public UpdateUserUseCase(
        IUserRepository userRepository,
        IUserReadRepository userReadRepository)
    {
        _userRepository = userRepository;
        _userReadRepository = userReadRepository;
    }


    /// <summary>
    /// Operação para atualizar o perfil do usuário autenticado.
    /// </summary>
    /// <param name="command">Comando com os dados da atualização.</param>
    public async Task Execute(UpdateUserCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await _userReadRepository.GetByUserIdentifierAsync(command.UserIdentifier);

        if (user is null || !user.IsActive)
            throw new NotFoundException("Usuário não encontrado.");

        user.UpdateProfile(command.FirstName, command.LastName, command.Contact);
        await _userRepository.UpdateAsync(user);
    }
}
