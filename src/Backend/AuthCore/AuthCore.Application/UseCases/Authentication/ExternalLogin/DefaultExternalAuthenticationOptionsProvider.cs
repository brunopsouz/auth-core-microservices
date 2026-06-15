namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa provedor padrao de configuracoes de autenticacao externa.
/// </summary>
internal sealed class DefaultExternalAuthenticationOptionsProvider : IExternalAuthenticationOptionsProvider
{
    /// <summary>
    /// Operacao para obter configuracoes do fluxo de autenticacao externa.
    /// </summary>
    /// <returns>Configuracoes seguras padrao.</returns>
    public ExternalAuthenticationOptions GetOptions()
    {
        return new ExternalAuthenticationOptions();
    }
}
