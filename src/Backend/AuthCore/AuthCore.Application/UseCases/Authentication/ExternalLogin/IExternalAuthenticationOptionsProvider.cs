namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Define operação para fornecer configurações de autenticação externa.
/// </summary>
public interface IExternalAuthenticationOptionsProvider
{
    /// <summary>
    /// Operação para obter configurações de autenticação externa.
    /// </summary>
    /// <returns>Configurações de autenticação externa.</returns>
    ExternalAuthenticationOptions GetOptions();
}
