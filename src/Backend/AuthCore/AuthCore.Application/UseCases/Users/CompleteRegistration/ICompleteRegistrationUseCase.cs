namespace AuthCore.Application.UseCases.Users.CompleteRegistration;

/// <summary>
/// Define operação para concluir o registro de usuário.
/// </summary>
public interface ICompleteRegistrationUseCase
{
    /// <summary>
    /// Operação para concluir o registro de usuário.
    /// </summary>
    /// <param name="command">Comando com e-mail, código OTP e senha.</param>
    Task Execute(CompleteRegistrationCommand command);
}
