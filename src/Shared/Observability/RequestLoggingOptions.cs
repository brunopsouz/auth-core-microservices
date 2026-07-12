namespace Shared.Observability;

/// <summary>
/// Representa as opções de registro de conclusão de requisições HTTP.
/// </summary>
public sealed class RequestLoggingOptions
{
    /// <summary>
    /// Indica se o middleware deve registrar conclusões de requisições.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Indica se requisições bem-sucedidas normais devem ser registradas.
    /// </summary>
    public bool LogSuccessfulRequests { get; set; }

    /// <summary>
    /// Indica se erros esperados de cliente devem ser registrados.
    /// </summary>
    public bool LogClientErrors { get; set; }

    /// <summary>
    /// Indica se respostas de rate limit devem ser registradas.
    /// </summary>
    public bool LogRateLimitedRequests { get; set; } = true;

    /// <summary>
    /// Indica se erros de servidor devem ser registrados.
    /// </summary>
    public bool LogServerErrors { get; set; } = true;

    /// <summary>
    /// Indica se health checks saudáveis devem ser registrados.
    /// </summary>
    public bool LogHealthChecks { get; set; }

    /// <summary>
    /// Indica se o identificador estável do usuário autenticado deve ser incluído.
    /// </summary>
    public bool IncludeUserId { get; set; }

    /// <summary>
    /// Representa o limite para classificar uma requisição como lenta.
    /// </summary>
    public double SlowRequestThresholdMilliseconds { get; set; } = 1_000d;
}
