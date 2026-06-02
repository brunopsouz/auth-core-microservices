namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa o status persistido da sessao.
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// Sessao ativa.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Sessao expirada.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// Sessao revogada.
    /// </summary>
    Revoked = 3
}
