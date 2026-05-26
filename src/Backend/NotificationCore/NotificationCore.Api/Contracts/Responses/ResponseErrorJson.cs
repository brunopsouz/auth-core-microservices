namespace NotificationCore.Api.Contracts.Responses;

/// <summary>
/// Representa resposta JSON para erros da API.
/// </summary>
public sealed class ResponseErrorJson
{
    /// <summary>
    /// Mensagens de erro retornadas para o cliente.
    /// </summary>
    public IList<string> Errors { get; init; } = [];
}
