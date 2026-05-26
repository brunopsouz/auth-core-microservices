namespace AuthCore.Application.UseCases.Authentication.RefreshSession;

/// <summary>
/// Representa comando para renovar uma autenticação do modo token.
/// </summary>
public sealed class RefreshSessionCommand
{
    /// <summary>
    /// Refresh token informado pelo cliente.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
