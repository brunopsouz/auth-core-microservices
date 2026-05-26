namespace AuthCore.Application.UseCases.Authentication.LogoutSession;

/// <summary>
/// Define operação para encerrar uma autenticação do modo token.
/// </summary>
public interface ILogoutSessionUseCase
{
    /// <summary>
    /// Operação para encerrar uma autenticação do modo token.
    /// </summary>
    /// <param name="command">Comando com o refresh token informado.</param>
    Task Execute(LogoutSessionCommand command);
}
