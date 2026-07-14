namespace Shared.Observability;

/// <summary>
/// Representa tags estáveis para health checks.
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    /// Tag de liveness.
    /// </summary>
    public const string Live = "live";

    /// <summary>
    /// Tag de readiness.
    /// </summary>
    public const string Ready = "ready";

    /// <summary>
    /// Tag de dependência técnica.
    /// </summary>
    public const string Dependency = "dependency";

    /// <summary>
    /// Tag de dependência crítica.
    /// </summary>
    public const string Critical = "critical";

    /// <summary>
    /// Tag de dependência opcional.
    /// </summary>
    public const string Optional = "optional";
}
