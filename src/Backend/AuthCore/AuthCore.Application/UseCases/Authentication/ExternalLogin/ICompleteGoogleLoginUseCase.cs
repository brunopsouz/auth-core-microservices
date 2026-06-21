namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Define operacao para concluir login com Google.
/// </summary>
public interface ICompleteGoogleLoginUseCase
{
    /// <summary>
    /// Operacao para concluir login com Google.
    /// </summary>
    /// <param name="command">Comando com dados externos do Google.</param>
    /// <param name="cancellationToken">Token de cancelamento da operacao.</param>
    /// <returns>Resultado da conclusao do login com Google.</returns>
    Task<CompleteGoogleLoginResult> Execute(
        CompleteGoogleLoginCommand command,
        CancellationToken cancellationToken = default);
}
