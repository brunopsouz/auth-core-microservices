namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Define operacao para vincular login Google ao usuario autenticado.
/// </summary>
public interface ILinkGoogleLoginUseCase
{
    /// <summary>
    /// Operacao para vincular login Google ao usuario autenticado.
    /// </summary>
    /// <param name="command">Comando com usuario autenticado e dados externos do Google.</param>
    Task Execute(LinkGoogleLoginCommand command);
}
