using AuthCore.Application.UseCases.Authentication.Models;

namespace AuthCore.Application.UseCases.Authentication.LoginSession;

/// <summary>
/// Define operação para autenticar um usuário por sessão.
/// </summary>
public interface ILoginSessionUseCase
{
    /// <summary>
    /// Operação para autenticar um usuário por sessão.
    /// </summary>
    /// <param name="command">Comando com as credenciais e metadados da sessão.</param>
    /// <returns>Resultado da autenticação por sessão.</returns>
    Task<AuthenticatedUserSessionResult> Execute(LoginSessionCommand command);
}
