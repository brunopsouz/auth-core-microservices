namespace Shared.Observability;

/// <summary>
/// Representa as opções de configuração da observabilidade do host.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>
    /// Indica se os providers do OpenTelemetry devem ser registrados.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Representa o nome estável do serviço.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Representa o namespace estável do serviço.
    /// </summary>
    public string ServiceNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Indica se o exporter OTLP deve ser registrado.
    /// </summary>
    public bool OtlpEnabled { get; set; }

    /// <summary>
    /// Representa o endpoint OTLP opcional do host.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Indica se exporters de console para traces e métricas devem ser registrados.
    /// </summary>
    public bool ConsoleExporterEnabled { get; set; }

    /// <summary>
    /// Representa a razão de amostragem de traces entre zero e um.
    /// </summary>
    public double TraceSamplingRatio { get; set; } = 0.1d;

    /// <summary>
    /// Indica se health checks deverão ser excluídos por instrumentações futuras.
    /// </summary>
    public bool ExcludeHealthChecks { get; set; } = true;
}
