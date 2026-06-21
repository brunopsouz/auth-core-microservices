using Npgsql;

namespace NotificationCore.Infrastructure.Abstractions.Data;

/// <summary>
/// Define operação para controlar o tempo de uso de uma conexão de banco.
/// </summary>
public interface IDatabaseConnectionLease : IAsyncDisposable
{
    /// <summary>
    /// Conexão PostgreSQL disponível durante o lease.
    /// </summary>
    NpgsqlConnection Connection { get; }
}
