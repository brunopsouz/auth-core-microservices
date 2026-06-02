namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa o motivo de revogacao da sessao.
/// </summary>
public enum SessionRevocationReason
{
    /// <summary>
    /// Revogacao por logout do usuario.
    /// </summary>
    UserLogout = 1,

    /// <summary>
    /// Revogacao de dispositivo pelo usuario.
    /// </summary>
    UserRevokedDevice = 2,

    /// <summary>
    /// Revogacao por troca de senha.
    /// </summary>
    PasswordChanged = 3
}
