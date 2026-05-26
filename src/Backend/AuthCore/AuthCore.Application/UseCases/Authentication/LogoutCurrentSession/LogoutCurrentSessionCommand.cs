namespace AuthCore.Application.UseCases.Authentication.LogoutCurrentSession;

/// <summary>
/// Representa o comando para encerrar a sessão atual.
/// </summary>
public sealed class LogoutCurrentSessionCommand
{
    /// <summary>
    /// Identificador público da sessão atual.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}
