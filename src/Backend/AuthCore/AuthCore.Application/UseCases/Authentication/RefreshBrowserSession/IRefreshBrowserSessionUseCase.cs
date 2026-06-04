using AuthCore.Application.UseCases.Authentication.Models;

namespace AuthCore.Application.UseCases.Authentication.RefreshBrowserSession;

/// <summary>
/// Define operacao para renovar access token por sessao autenticada do browser.
/// </summary>
public interface IRefreshBrowserSessionUseCase
{
    /// <summary>
    /// Operacao para renovar o access token curto da sessao autenticada.
    /// </summary>
    /// <param name="command">Comando com o identificador opaco da sessao.</param>
    /// <returns>Resultado da renovacao do access token.</returns>
    Task<RefreshedSessionAccessResult> Execute(RefreshBrowserSessionCommand command);
}
