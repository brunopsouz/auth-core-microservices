namespace AuthCore.Api.Security;

/// <summary>
/// Define operacao para validar origem confiavel de uma requisicao.
/// </summary>
public interface ITrustedOriginValidator
{
    /// <summary>
    /// Operacao para validar origem declarada na requisicao HTTP atual.
    /// </summary>
    /// <param name="request">Requisicao HTTP atual.</param>
    void Validate(HttpRequest request);
}
