using System.Diagnostics;
using System.Data;
using NotificationCore.Infrastructure.Abstractions.Data;
using NotificationCore.Infrastructure.Observability;
using Npgsql;

namespace NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.Connections;

/// <summary>
/// Representa a fábrica de conexões PostgreSQL.
/// </summary>
internal sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    /// <summary>
    /// Campo que armazena a fonte de dados PostgreSQL.
    /// </summary>
    private readonly NpgsqlDataSource _dataSource;
    private readonly DatabaseMetrics _metrics;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="dataSource">Fonte de dados PostgreSQL compartilhada.</param>
    public NpgsqlConnectionFactory(NpgsqlDataSource dataSource, DatabaseMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(metrics);
        _dataSource = dataSource;
        _metrics = metrics;
    }


    /// <summary>
    /// Operação para criar uma conexão aberta com o banco de dados.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Conexão aberta pronta para uso.</returns>
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var succeeded = false;
        try
        {
            var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            succeeded = true;

            return connection;
        }
        catch (Exception exception)
        {
            _metrics.RecordAcquisitionFailure(exception);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordAcquisition(stopwatch.Elapsed, succeeded);
        }
    }
}
