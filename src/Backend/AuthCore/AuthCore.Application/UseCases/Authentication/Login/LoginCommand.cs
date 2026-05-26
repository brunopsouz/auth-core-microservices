namespace AuthCore.Application.UseCases.Authentication.Login;

/// <summary>
/// Representa comando para autenticar um usuário.
/// </summary>
public sealed class LoginCommand
{
    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Senha do usuário.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
