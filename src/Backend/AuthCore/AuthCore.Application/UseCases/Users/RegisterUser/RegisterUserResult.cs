namespace AuthCore.Application.UseCases.Users.RegisterUser;

/// <summary>
/// Representa resultado do usuário registrado.
/// </summary>
public sealed class RegisterUserResult
{
    /// <summary>
    /// Identificador público do usuário.
    /// </summary>
    public Guid UserIdentifier { get; set; }

    /// <summary>
    /// Nome completo do usuário.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public string Email { get; set; } = string.Empty;
}
