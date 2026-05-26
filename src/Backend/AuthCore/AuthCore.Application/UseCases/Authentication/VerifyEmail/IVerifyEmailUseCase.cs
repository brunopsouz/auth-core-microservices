namespace AuthCore.Application.UseCases.Authentication.VerifyEmail;

/// <summary>
/// Define operação para verificar o e-mail do usuário.
/// </summary>
public interface IVerifyEmailUseCase
{
    /// <summary>
    /// Operação para verificar o e-mail do usuário.
    /// </summary>
    /// <param name="command">Comando com o e-mail e código OTP.</param>
    Task Execute(VerifyEmailCommand command);
}
