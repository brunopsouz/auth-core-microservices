namespace AuthCore.Application.UseCases.Users.UpdateUser;

/// <summary>
/// Define operação para atualizar o perfil do usuário autenticado.
/// </summary>
public interface IUpdateUserUseCase
{
    /// <summary>
    /// Operação para atualizar o perfil do usuário autenticado.
    /// </summary>
    /// <param name="command">Comando com os dados da atualização.</param>
    Task Execute(UpdateUserCommand command);
}
