namespace AuthCore.Api.Authentication;

/// <summary>
/// Define operacoes do ticket temporario de onboarding Google.
/// </summary>
public interface IGoogleOnboardingTicketStore
{
    /// <summary>
    /// Operacao para emitir ticket temporario de onboarding Google.
    /// </summary>
    /// <param name="response">Resposta HTTP que recebera o cookie.</param>
    /// <param name="command">Comando com dados verificados do Google.</param>
    void Append(HttpResponse response, CompleteGoogleOnboardingTicketCommand command);

    /// <summary>
    /// Operacao para ler ticket temporario de onboarding Google.
    /// </summary>
    /// <param name="request">Requisicao HTTP com o cookie protegido.</param>
    /// <returns>Ticket encontrado ou nulo.</returns>
    GoogleOnboardingTicket? Read(HttpRequest request);

    /// <summary>
    /// Operacao para remover ticket temporario de onboarding Google.
    /// </summary>
    /// <param name="response">Resposta HTTP que removera o cookie.</param>
    void Delete(HttpResponse response);
}

/// <summary>
/// Representa comando para emitir ticket temporario de onboarding Google.
/// </summary>
public sealed record CompleteGoogleOnboardingTicketCommand(
    string ProviderUserId,
    string Email,
    bool EmailVerified,
    string? FullName,
    string? PictureUrl);
