namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa ticket temporario para concluir cadastro com Google.
/// </summary>
public sealed record GoogleOnboardingTicket(
    string ProviderUserId,
    string Email,
    bool EmailVerified,
    string FullName,
    string PictureUrl,
    DateTime ExpiresAtUtc);
