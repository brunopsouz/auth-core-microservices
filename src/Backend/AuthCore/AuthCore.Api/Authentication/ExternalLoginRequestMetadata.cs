namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa metadados HTTP da requisicao de login externo.
/// </summary>
/// <param name="IpAddress">Endereco IP de origem da requisicao.</param>
/// <param name="UserAgent">Identificacao do agente que iniciou a requisicao.</param>
internal sealed record ExternalLoginRequestMetadata(
    string? IpAddress,
    string? UserAgent);
