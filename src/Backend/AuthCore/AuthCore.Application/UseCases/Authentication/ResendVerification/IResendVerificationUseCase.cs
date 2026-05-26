namespace AuthCore.Application.UseCases.Authentication.ResendVerification;

/// <summary>
/// Define operação para reenviar a verificação de e-mail.
/// </summary>
public interface IResendVerificationUseCase
{
    /// <summary>
    /// Operação para reenviar a verificação de e-mail.
    /// </summary>
    /// <param name="command">Comando com o e-mail alvo.</param>
    Task Execute(ResendVerificationCommand command);
}
