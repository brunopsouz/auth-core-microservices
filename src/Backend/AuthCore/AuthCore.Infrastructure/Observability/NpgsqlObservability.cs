namespace AuthCore.Infrastructure.Observability;

/// <summary>
/// Representa nomes técnicos da observabilidade PostgreSQL.
/// </summary>
internal static class NpgsqlObservability
{
    /// <summary>
    /// Nome lógico da fonte de dados PostgreSQL.
    /// </summary>
    public const string DataSourceName = "authcore-postgresql";

    /// <summary>
    /// Nome do meter nativo emitido pelo Npgsql.
    /// </summary>
    public const string MeterName = "Npgsql";
}
