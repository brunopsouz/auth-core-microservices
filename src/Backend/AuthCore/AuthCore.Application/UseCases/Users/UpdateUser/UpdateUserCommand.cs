namespace AuthCore.Application.UseCases.Users.UpdateUser;

/// <summary>
/// Representa comando para atualizar o perfil do usuário autenticado.
/// </summary>
public sealed class UpdateUserCommand
{
    /// <summary>
    /// Identificador público do usuário autenticado.
    /// </summary>
    public Guid UserIdentifier { get; set; }

    /// <summary>
    /// Primeiro nome do usuário.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Sobrenome do usuário.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Número de contato do usuário.
    /// </summary>
    public string Contact { get; set; } = string.Empty;
}
