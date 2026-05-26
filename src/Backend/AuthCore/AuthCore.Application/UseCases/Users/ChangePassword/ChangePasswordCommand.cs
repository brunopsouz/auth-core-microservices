namespace AuthCore.Application.UseCases.Users.ChangePassword;

/// <summary>
/// Representa comando para alterar a senha do usuário autenticado.
/// </summary>
public sealed class ChangePasswordCommand
{
    /// <summary>
    /// Identificador público do usuário autenticado.
    /// </summary>
    public Guid UserIdentifier { get; set; }

    /// <summary>
    /// Senha atual do usuário.
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// Nova senha do usuário.
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// Confirmação da nova senha do usuário.
    /// </summary>
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
