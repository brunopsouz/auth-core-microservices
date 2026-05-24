namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operações para cálculo de expiração de sessão.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Indica se a sessão usa expiração deslizante.
    /// </summary>
    bool UseSlidingExpiration { get; }

    /// <summary>
    /// Operação para obter a expiração inicial de uma sessão.
    /// </summary>
    /// <returns>Data de expiração em UTC.</returns>
    DateTime GetExpiresAtUtc();

    /// <summary>
    /// Operação para obter a nova expiração deslizante a partir de um instante de referência.
    /// </summary>
    /// <param name="referenceAtUtc">Instante de referência em UTC.</param>
    /// <returns>Data de expiração em UTC.</returns>
    DateTime GetSlidingExpiresAtUtc(DateTime referenceAtUtc);
}
