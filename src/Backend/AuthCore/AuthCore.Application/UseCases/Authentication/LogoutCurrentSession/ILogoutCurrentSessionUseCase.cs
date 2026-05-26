namespace AuthCore.Application.UseCases.Authentication.LogoutCurrentSession;

/// <summary>
/// Define operação para encerrar a sessão atual do usuário.
/// </summary>
public interface ILogoutCurrentSessionUseCase
{
    /// <summary>
    /// Operação para encerrar a sessão atual do usuário.
    /// </summary>
    /// <param name="command">Comando com a sessão atual.</param>
    Task Execute(LogoutCurrentSessionCommand command);
}
