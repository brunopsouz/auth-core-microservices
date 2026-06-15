namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Define operacao para desvincular login Google do usuario autenticado.
/// </summary>
public interface IUnlinkGoogleLoginUseCase
{
    /// <summary>
    /// Operacao para desvincular login Google do usuario autenticado.
    /// </summary>
    /// <param name="command">Comando com usuario autenticado.</param>
    Task Execute(UnlinkGoogleLoginCommand command);
}
