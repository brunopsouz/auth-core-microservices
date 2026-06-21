namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa resultado do fluxo de autenticacao externa com Google.
/// </summary>
/// <param name="RedirectUrl">URL para redirecionamento apos a conclusao do fluxo.</param>
public sealed record GoogleExternalAuthenticationResult(string RedirectUrl);
