namespace AuthCore.Domain.Users;

/// <summary>
/// Representa o status funcional do usuário.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// Usuário pendente de verificação de e-mail.
    /// </summary>
    PendingEmailVerification = 0,

    /// <summary>
    /// Usuário ativo para autenticação.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Usuário bloqueado para autenticação.
    /// </summary>
    Blocked = 2
}
