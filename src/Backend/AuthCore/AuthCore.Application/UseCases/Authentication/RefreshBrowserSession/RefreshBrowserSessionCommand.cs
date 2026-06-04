namespace AuthCore.Application.UseCases.Authentication.RefreshBrowserSession;

/// <summary>
/// Representa o comando para renovar access token por sessao autenticada.
/// </summary>
public sealed class RefreshBrowserSessionCommand
{
    /// <summary>
    /// Identificador opaco da sessao autenticada.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}
