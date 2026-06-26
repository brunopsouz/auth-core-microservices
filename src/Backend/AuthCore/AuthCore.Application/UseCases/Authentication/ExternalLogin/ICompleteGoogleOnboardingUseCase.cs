using AuthCore.Application.UseCases.Authentication.Models;

namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Define operacao para concluir onboarding de cadastro com Google.
/// </summary>
public interface ICompleteGoogleOnboardingUseCase
{
    /// <summary>
    /// Operacao para concluir onboarding de cadastro com Google.
    /// </summary>
    /// <param name="command">Comando com dados verificados do Google e complemento cadastral.</param>
    /// <param name="cancellationToken">Token de cancelamento da operacao.</param>
    /// <returns>Sessao autenticada emitida apos o cadastro.</returns>
    Task<AuthenticatedUserSessionResult> Execute(
        CompleteGoogleOnboardingCommand command,
        CancellationToken cancellationToken = default);
}
