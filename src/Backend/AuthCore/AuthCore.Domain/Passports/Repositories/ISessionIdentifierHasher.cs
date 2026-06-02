using AuthCore.Domain.Passports;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operacao para calcular hash de identificador opaco de sessao.
/// </summary>
public interface ISessionIdentifierHasher
{
    /// <summary>
    /// Operacao para calcular hash do identificador opaco de sessao.
    /// </summary>
    /// <param name="identifier">Identificador opaco de sessao.</param>
    /// <returns>Hash do identificador opaco.</returns>
    string ComputeHash(SessionIdentifier identifier);
}
