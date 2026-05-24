using AuthCore.Infrastructure.Abstractions.Data;
using AuthCore.Infrastructure.Services.Messaging;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL da outbox.
/// </summary>
internal sealed class OutboxRepository : IOutboxMessageRepository
{
    private readonly IDatabaseSession _databaseSession;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public OutboxRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }

    /// <summary>
    /// Operação para adicionar uma mensagem de outbox.
    /// </summary>
    /// <param name="message">Mensagem a ser persistida.</param>
    public async Task AddAsync(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        const string sql = """
            INSERT INTO "OutboxMessages"
            (
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "Type",
                "Content",
                "OccurredAtUtc",
                "ProcessedAtUtc",
                "AttemptCount",
                "LastError"
            )
            VALUES
            (
                @Id,
                @CreatedAt,
                @UpdateAt,
                @IsActive,
                @Type,
                @Content,
                @OccurredAtUtc,
                @ProcessedAtUtc,
                @AttemptCount,
                @LastError
            );
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        AddParameters(command, message);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para obter mensagens pendentes de processamento.
    /// </summary>
    /// <param name="take">Quantidade máxima de mensagens.</param>
    /// <param name="maxAttempts">Quantidade máxima de tentativas permitidas.</param>
    /// <returns>Coleção de mensagens pendentes.</returns>
    public async Task<IReadOnlyCollection<OutboxMessage>> GetPendingAsync(int take, int maxAttempts)
    {
        const string sql = """
            SELECT
                "Id",
                "Type",
                "Content",
                "OccurredAtUtc",
                "ProcessedAtUtc",
                "AttemptCount",
                "LastError"
            FROM "OutboxMessages"
            WHERE "ProcessedAtUtc" IS NULL
                AND "AttemptCount" < @MaxAttempts
            ORDER BY "OccurredAtUtc" ASC
            LIMIT @Take
            FOR UPDATE SKIP LOCKED;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("Take", take);
        command.Parameters.AddWithValue("MaxAttempts", maxAttempts);

        await using var reader = await command.ExecuteReaderAsync();
        var messages = new List<OutboxMessage>();

        while (await reader.ReadAsync())
        {
            messages.Add(OutboxMessage.Restore(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Type")),
                reader.GetString(reader.GetOrdinal("Content")),
                reader.GetDateTime(reader.GetOrdinal("OccurredAtUtc")),
                reader.IsDBNull(reader.GetOrdinal("ProcessedAtUtc"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("ProcessedAtUtc")),
                reader.GetInt32(reader.GetOrdinal("AttemptCount")),
                reader.IsDBNull(reader.GetOrdinal("LastError"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("LastError"))));
        }

        return messages;
    }

    /// <summary>
    /// Operação para atualizar uma mensagem de outbox.
    /// </summary>
    /// <param name="message">Mensagem atualizada.</param>
    public async Task UpdateAsync(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        const string sql = """
            UPDATE "OutboxMessages"
            SET
                "UpdateAt" = @UpdateAt,
                "ProcessedAtUtc" = @ProcessedAtUtc,
                "AttemptCount" = @AttemptCount,
                "LastError" = @LastError
            WHERE "Id" = @Id;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        AddParameters(command, message, includeStaticColumns: false);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para adicionar os parâmetros da outbox ao comando SQL.
    /// </summary>
    /// <param name="command">Comando SQL alvo.</param>
    /// <param name="message">Mensagem persistida.</param>
    /// <param name="includeStaticColumns">Indica se os campos imutáveis devem ser adicionados.</param>
    private static void AddParameters(NpgsqlCommand command, OutboxMessage message, bool includeStaticColumns = true)
    {
        command.Parameters.AddWithValue("Id", message.Id);

        if (includeStaticColumns)
        {
            command.Parameters.AddWithValue("CreatedAt", message.OccurredAtUtc);
            command.Parameters.AddWithValue("UpdateAt", message.OccurredAtUtc);
            command.Parameters.AddWithValue("IsActive", true);
            command.Parameters.AddWithValue("Type", message.Type);
            command.Parameters.AddWithValue("Content", message.Content);
            command.Parameters.AddWithValue("OccurredAtUtc", message.OccurredAtUtc);
        }
        else
        {
            command.Parameters.AddWithValue("UpdateAt", DateTime.UtcNow);
        }

        command.Parameters.AddWithValue("ProcessedAtUtc", message.ProcessedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("AttemptCount", message.AttemptCount);
        command.Parameters.AddWithValue("LastError", message.LastError ?? (object)DBNull.Value);
    }

    /// <summary>
    /// Operação para criar comando SQL respeitando a transação atual.
    /// </summary>
    /// <param name="connection">Conexão aberta da sessão.</param>
    /// <param name="sql">Comando SQL a ser executado.</param>
    /// <returns>Comando pronto para uso.</returns>
    private NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql)
    {
        return new NpgsqlCommand(sql, connection, _databaseSession.CurrentTransaction);
    }

}
