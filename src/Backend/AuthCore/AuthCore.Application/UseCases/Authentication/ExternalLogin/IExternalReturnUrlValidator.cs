namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Define operação para validar URL de retorno de autenticação externa.
/// </summary>
public interface IExternalReturnUrlValidator
{
    /// <summary>
    /// Operação para validar e normalizar URL de retorno.
    /// </summary>
    /// <param name="returnUrl">URL de retorno informada pelo cliente.</param>
    /// <returns>URL de retorno segura.</returns>
    string Validate(string? returnUrl);
}
