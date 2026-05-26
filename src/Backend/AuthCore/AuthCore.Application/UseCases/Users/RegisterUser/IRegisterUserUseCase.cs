namespace AuthCore.Application.UseCases.Users.RegisterUser;

/// <summary>
/// Define operação para registrar um usuário.
/// </summary>
public interface IRegisterUserUseCase
{
    /// <summary>
    /// Operação para registrar um usuário.
    /// </summary>
    /// <param name="command">Comando com os dados do registro.</param>
    /// <returns>Resultado do usuário registrado.</returns>
    Task<RegisterUserResult> Execute(RegisterUserCommand command);
}
