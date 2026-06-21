using System.Diagnostics;
using NotificationCore.Infrastructure.Abstractions.Data;
using NotificationCore.Infrastructure.Observability;
using Npgsql;

namespace NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.Connections;

/// <summary>
/// Representa o tempo de uso controlado de uma conexão PostgreSQL.
/// </summary>
internal sealed class DatabaseConnectionLease : IDatabaseConnectionLease
{
    private readonly bool _ownsConnection;
    private readonly DatabaseMetrics _metrics;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="connection">Conexão PostgreSQL aberta.</param>
    /// <param name="ownsConnection">Indica se o lease deve devolver a conexão ao pool.</param>
    public DatabaseConnectionLease(
        NpgsqlConnection connection,
        bool ownsConnection,
        DatabaseMetrics metrics)
    {
        Connection = connection;
        _ownsConnection = ownsConnection;
        _metrics = metrics;
    }

    /// <inheritdoc />
    public NpgsqlConnection Connection { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_ownsConnection)
            return;

        await Connection.DisposeAsync();
        _stopwatch.Stop();
        _metrics.RecordConnectionLease(_stopwatch.Elapsed);
    }
}
