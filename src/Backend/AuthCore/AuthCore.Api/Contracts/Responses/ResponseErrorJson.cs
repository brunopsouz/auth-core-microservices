namespace AuthCore.Api.Contracts.Responses;

/// <summary>
/// Representa resposta de erro da aplicacao.
/// </summary>
public sealed class ResponseErrorJson
{
    /// <summary>
    /// Lista de mensagens de erro retornadas pela aplicacao.
    /// </summary>
    public IList<string> Errors { get; set; } = [];
}
