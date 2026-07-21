namespace NotificationCore.Infrastructure.Observability;

using OpenTelemetry.Metrics;

/// <summary>
/// Representa nomes técnicos da observabilidade PostgreSQL.
/// </summary>
internal static class NpgsqlObservability
{
    private static readonly string[] MetricTagKeys =
    [
        "db.npgsql.data_source",
        "pool.name",
        "db.system.name",
        "server.address",
        "server.port",
        "error.type",
        "db.response.status_code"
    ];

    /// <summary>
    /// Nome lógico da fonte de dados PostgreSQL.
    /// </summary>
    public const string DataSourceName = "notificationcore-postgresql";

    /// <summary>
    /// Nome do meter nativo emitido pelo Npgsql.
    /// </summary>
    public const string MeterName = "Npgsql";

    /// <summary>
    /// Operação para criar configuração segura das métricas Npgsql.
    /// </summary>
    /// <returns>Configuração de stream de métrica.</returns>
    public static MetricStreamConfiguration CreateMetricView()
    {
        return new MetricStreamConfiguration
        {
            TagKeys = MetricTagKeys
        };
    }
}
