namespace AuthCore.Domain.Common.Enums;

/// <summary>
/// Enum responsável pelos status do password.
/// </summary>
public enum PasswordStatus
{
    /// <summary>
    /// Primeiro acesso.
    /// </summary>
    FirstAccess = 0,

    /// <summary>
    /// Status ativo.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Status bloqueado.
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// Status desativado.
    /// </summary>
    Deactivated = 3,

    /// <summary>
    /// Status removed para softdelete.
    /// </summary>
    Removed = 4   
       
}
