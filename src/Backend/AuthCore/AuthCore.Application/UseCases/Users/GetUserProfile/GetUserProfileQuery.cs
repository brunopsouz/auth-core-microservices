namespace AuthCore.Application.UseCases.Users.GetUserProfile;

/// <summary>
/// Representa consulta para obter o perfil do usuário autenticado.
/// </summary>
public sealed class GetUserProfileQuery
{
    /// <summary>
    /// Identificador público do usuário autenticado.
    /// </summary>
    public Guid UserIdentifier { get; set; }
}
