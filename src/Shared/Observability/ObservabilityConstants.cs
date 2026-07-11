namespace Shared.Observability;

/// <summary>
/// Representa constantes da configuração transversal de observabilidade.
/// </summary>
public static class ObservabilityConstants
{
    /// <summary>
    /// Representa o nome da seção de configuração.
    /// </summary>
    public const string ConfigurationSectionName = "Observability";

    /// <summary>
    /// Representa o nome da variável de ambiente do endpoint OTLP.
    /// </summary>
    public const string OtlpEndpointEnvironmentVariableName = "OTEL_EXPORTER_OTLP_ENDPOINT";
}
