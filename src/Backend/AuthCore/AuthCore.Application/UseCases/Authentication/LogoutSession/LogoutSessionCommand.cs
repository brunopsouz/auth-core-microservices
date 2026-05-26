namespace AuthCore.Application.UseCases.Authentication.LogoutSession;

/// <summary>
/// Representa comando para encerrar uma autenticação do modo token.
/// </summary>
public sealed class LogoutSessionCommand
{
    /// <summary>
    /// Refresh token informado pelo cliente.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
