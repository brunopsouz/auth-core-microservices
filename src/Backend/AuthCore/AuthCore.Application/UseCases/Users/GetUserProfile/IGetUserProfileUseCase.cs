namespace AuthCore.Application.UseCases.Users.GetUserProfile;

/// <summary>
/// Define operação para obter o perfil do usuário autenticado.
/// </summary>
public interface IGetUserProfileUseCase
{
    /// <summary>
    /// Operação para obter o perfil do usuário autenticado.
    /// </summary>
    /// <param name="query">Consulta do perfil do usuário.</param>
    /// <returns>Resultado com os dados do perfil do usuário.</returns>
    Task<GetUserProfileResult> Execute(GetUserProfileQuery query);
}
