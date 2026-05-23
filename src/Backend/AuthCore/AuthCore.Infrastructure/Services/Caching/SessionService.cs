using AuthCore.Domain.Passports.Services;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace AuthCore.Infrastructure.Services.Caching;

/// <summary>
/// Representa serviço para cálculo de expiração de sessão.
/// </summary>
internal sealed class SessionService : ISessionService
{
    private readonly SessionOptions _sessionOptions;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="sessionOptions">Configurações de sessão.</param>
    public SessionService(IOptions<SessionOptions> sessionOptions)
    {
        ArgumentNullException.ThrowIfNull(sessionOptions);

        _sessionOptions = sessionOptions.Value;
    }

    #endregion

    /// <summary>
    /// Indica se a sessão usa expiração deslizante.
    /// </summary>
    public bool UseSlidingExpiration => _sessionOptions.SlidingTtl;

    /// <summary>
    /// Operação para obter a expiração inicial de uma sessão.
    /// </summary>
    /// <returns>Data de expiração em UTC.</returns>
    public DateTime GetExpiresAtUtc()
    {
        return DateTime.UtcNow.AddMinutes(_sessionOptions.TtlMinutes);
    }

    /// <summary>
    /// Operação para obter a nova expiração deslizante.
    /// </summary>
    /// <param name="referenceAtUtc">Instante de referência em UTC.</param>
    /// <returns>Data de expiração em UTC.</returns>
    public DateTime GetSlidingExpiresAtUtc(DateTime referenceAtUtc)
    {
        return referenceAtUtc.AddMinutes(_sessionOptions.TtlMinutes);
    }
}
