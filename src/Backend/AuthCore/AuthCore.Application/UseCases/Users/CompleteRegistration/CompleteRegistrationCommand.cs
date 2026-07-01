namespace AuthCore.Application.UseCases.Users.CompleteRegistration;

/// <summary>
/// Representa comando para concluir o registro de usuário.
/// </summary>
public sealed class CompleteRegistrationCommand
{
    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Código OTP informado.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Senha informada para cadastro.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Confirmação da senha informada para cadastro.
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}
