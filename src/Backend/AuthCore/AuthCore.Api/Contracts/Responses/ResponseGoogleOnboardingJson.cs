namespace AuthCore.Api.Contracts.Responses;

/// <summary>
/// Representa resposta com dados seguros do onboarding Google.
/// </summary>
public sealed class ResponseGoogleOnboardingJson
{
    /// <summary>
    /// E-mail confirmado pelo Google.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Nome completo retornado pelo Google.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// URL da foto de perfil retornada pelo Google.
    /// </summary>
    public string PictureUrl { get; set; } = string.Empty;
}
