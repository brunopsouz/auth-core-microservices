namespace AuthCore.Api.Security;

/// <summary>
/// Define operacoes para gerar e validar token CSRF vinculado a sessao.
/// </summary>
public interface ICsrfTokenService
{
    /// <summary>
    /// Operacao para gerar token CSRF vinculado ao identificador opaco da sessao.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao autenticada.</param>
    /// <returns>Token CSRF assinado.</returns>
    string Generate(string sessionId);

    /// <summary>
    /// Operacao para validar token CSRF vinculado ao identificador opaco da sessao.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao autenticada.</param>
    /// <param name="token">Token CSRF informado.</param>
    /// <returns><c>true</c> quando o token e valido; caso contrario, <c>false</c>.</returns>
    bool IsValid(string sessionId, string token);
}
