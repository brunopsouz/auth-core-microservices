namespace AuthCore.Application.UseCases.Users.ChangePassword;

/// <summary>
/// Define operação para alterar a senha do usuário autenticado.
/// </summary>
public interface IChangePasswordUseCase
{
    /// <summary>
    /// Operação para alterar a senha do usuário autenticado.
    /// </summary>
    /// <param name="command">Comando da alteração de senha.</param>
    Task Execute(ChangePasswordCommand command);
}
