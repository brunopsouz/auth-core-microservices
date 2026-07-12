namespace Shared.Observability;

/// <summary>
/// Representa constantes do log de conclusão HTTP.
/// </summary>
public static class RequestLoggingConstants
{
    /// <summary>
    /// Representa a chave usada para armazenar a categoria segura de erro.
    /// </summary>
    public const string ErrorCategoryItemKey = "Shared.Observability.ErrorCategory";

    /// <summary>
    /// Representa a rota usada quando nenhum template seguro foi identificado.
    /// </summary>
    public const string UnmatchedRoute = "unmatched";

    /// <summary>
    /// Representa a rota segura usada para encaminhamentos Ocelot.
    /// </summary>
    public const string OcelotDownstreamRoute = "ocelot-downstream";
}
