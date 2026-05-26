using AuthCore.Application.UseCases.Authentication.Models;

namespace AuthCore.Application.UseCases.Authentication.Login;

/// <summary>
/// Define operação para autenticar um usuário no modo token.
/// </summary>
public interface ILoginUseCase
{
    /// <summary>
    /// Operação para autenticar um usuário no modo token.
    /// </summary>
    /// <param name="command">Comando com as credenciais do login.</param>
    /// <returns>Resultado da sessão autenticada.</returns>
    Task<AuthenticatedSessionResult> Execute(LoginCommand command);
}
