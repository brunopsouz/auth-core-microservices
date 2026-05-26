namespace AuthCore.Application.UseCases.Authentication.VerifyEmail;

/// <summary>
/// Representa o comando de verificação de e-mail.
/// </summary>
public sealed class VerifyEmailCommand
{
    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Código OTP informado.
    /// </summary>
    public string Code { get; init; } = string.Empty;
}
