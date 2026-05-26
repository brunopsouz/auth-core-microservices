namespace AuthCore.Application.UseCases.Users.RegisterUser;

/// <summary>
/// Representa comando para registrar um usuário.
/// </summary>
public sealed class RegisterUserCommand
{
    /// <summary>
    /// Primeiro nome do usuário.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Sobrenome do usuário.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Número de contato do usuário.
    /// </summary>
    public string Contact { get; set; } = string.Empty;

    /// <summary>
    /// Senha informada para cadastro.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Confirmação da senha informada para cadastro.
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}
