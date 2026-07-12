namespace Shared.Observability;

/// <summary>
/// Representa constantes do identificador de correlação HTTP.
/// </summary>
public static class CorrelationIdConstants
{
    /// <summary>
    /// Representa o nome do header HTTP de correlação.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// Representa a chave usada para armazenar o valor no contexto HTTP.
    /// </summary>
    public const string HttpContextItemKey = "Shared.Observability.CorrelationId";

    /// <summary>
    /// Representa o nome da propriedade do escopo de log.
    /// </summary>
    public const string LogScopePropertyName = "CorrelationId";

    /// <summary>
    /// Representa o nome da tag adicionada à atividade atual.
    /// </summary>
    public const string ActivityTagName = "correlation.id";
}
