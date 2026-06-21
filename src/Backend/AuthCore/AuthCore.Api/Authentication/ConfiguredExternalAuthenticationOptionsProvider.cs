using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using Microsoft.Extensions.Configuration;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa provedor de configurações de autenticação externa a partir da configuração da API.
/// </summary>
internal sealed class ConfiguredExternalAuthenticationOptionsProvider : IExternalAuthenticationOptionsProvider
{
    /// <summary>
    /// Campo que armazena configuration.
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="configuration">Configuração da aplicação.</param>
    public ConfiguredExternalAuthenticationOptionsProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _configuration = configuration;
    }

    /// <summary>
    /// Operação para obter configurações de autenticação externa.
    /// </summary>
    /// <returns>Configurações de autenticação externa.</returns>
    public ExternalAuthenticationOptions GetOptions()
    {
        return _configuration
            .GetSection(ExternalAuthenticationOptions.SectionName)
            .Get<ExternalAuthenticationOptions>()
            ?? new ExternalAuthenticationOptions();
    }
}
