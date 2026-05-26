namespace AuthCore.Application.UseCases.Users.GetUserProfile;

/// <summary>
/// Representa resultado do perfil do usuário.
/// </summary>
public sealed class GetUserProfileResult
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
    /// Nome completo do usuário.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Número de contato do usuário.
    /// </summary>
    public string Contact { get; set; } = string.Empty;

    /// <summary>
    /// Perfil do usuário.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Indica se o e-mail do usuário foi verificado.
    /// </summary>
    public bool IsEmailVerified { get; set; }
}
