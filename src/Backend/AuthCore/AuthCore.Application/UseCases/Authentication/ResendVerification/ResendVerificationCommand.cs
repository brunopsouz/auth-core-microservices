namespace AuthCore.Application.UseCases.Authentication.ResendVerification;

/// <summary>
/// Representa o comando de reenvio da verificação de e-mail.
/// </summary>
public sealed class ResendVerificationCommand
{
    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public string Email { get; init; } = string.Empty;
}
