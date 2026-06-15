namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa comando para desvincular login Google do usuario autenticado.
/// </summary>
public sealed class UnlinkGoogleLoginCommand
{
    /// <summary>
    /// Identificador publico do usuario autenticado.
    /// </summary>
    public Guid UserIdentifier { get; init; }
}
