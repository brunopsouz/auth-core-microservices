using BuildingBlocks.Messaging.Contracts.Security;
using NotificationCore.Domain.Common.Messaging;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL da inbox.
/// </summary>
internal sealed class InboxRepository : IInboxRepository
{
    /// <summary>
    /// Campo que armazena database session.
    /// </summary>
    private readonly IDatabaseSession _databaseSession;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public InboxRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }


    /// <summary>
    /// Operação para adicionar uma mensagem de inbox.
    /// </summary>
    /// <param name="message">Mensagem a ser persistida.</param>
    public async Task AddAsync(InboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        const string sql = """
            INSERT INTO "InboxMessages"
            (
                "MessageId",
                "Source",
                "Type",
                "Payload",
                "ReceivedAtUtc",
                "ProcessedAtUtc",
                "Status",
                "Error"
            )
            VALUES
            (
                @MessageId,
                @Source,
                @Type,
                @Payload,
                @ReceivedAtUtc,
                @ProcessedAtUtc,
                @Status,
                @Error
            );
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        AddParameters(command, message);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para tentar adicionar uma mensagem de inbox de forma idempotente.
    /// </summary>
    /// <param name="message">Mensagem a ser persistida.</param>
    /// <returns>Verdadeiro quando a mensagem foi adicionada.</returns>
    public async Task<bool> TryAddAsync(InboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        const string sql = """
            INSERT INTO "InboxMessages"
            (
                "MessageId",
                "Source",
                "Type",
                "Payload",
                "ReceivedAtUtc",
                "ProcessedAtUtc",
                "Status",
                "Error"
            )
            VALUES
            (
                @MessageId,
                @Source,
                @Type,
                @Payload,
                @ReceivedAtUtc,
                @ProcessedAtUtc,
                @Status,
                @Error
            )
            ON CONFLICT ("MessageId") DO NOTHING;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        AddParameters(command, message);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>
    /// Operação para obter uma mensagem pelo identificador idempotente.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <returns>Mensagem encontrada ou nula.</returns>
    public async Task<InboxMessage?> GetByMessageIdAsync(Guid messageId)
    {
        const string sql = """
            SELECT
                "MessageId",
                "Source",
                "Type",
                "Payload",
                "ReceivedAtUtc",
                "ProcessedAtUtc",
                "Status",
                "Error"
            FROM "InboxMessages"
            WHERE "MessageId" = @MessageId
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("MessageId", messageId);

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadMessageAsync(reader);
    }

    /// <summary>
    /// Operação para obter a mensagem original pela chave de idempotência da notificação.
    /// </summary>
    /// <param name="idempotencyKey">Chave de idempotência da notificação.</param>
    /// <returns>Mensagem encontrada ou nula.</returns>
    public async Task<InboxMessage?> GetByNotificationIdempotencyKeyAsync(string idempotencyKey)
    {
        const string sql = """
            SELECT
                "MessageId",
                "Source",
                "Type",
                "Payload",
                "ReceivedAtUtc",
                "ProcessedAtUtc",
                "Status",
                "Error"
            FROM "InboxMessages"
            WHERE "Payload"::jsonb ->> 'IdempotencyKey' = @IdempotencyKey
            ORDER BY "ReceivedAtUtc" ASC
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("IdempotencyKey", idempotencyKey.Trim());

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadMessageAsync(reader);
    }

    /// <summary>
    /// Operação para obter mensagens recebidas e ainda não processadas.
    /// </summary>
    /// <param name="take">Quantidade máxima de mensagens.</param>
    /// <returns>Coleção de mensagens pendentes.</returns>
    public async Task<IReadOnlyCollection<InboxMessage>> GetPendingAsync(int take)
    {
        const string sql = """
            SELECT
                "MessageId",
                "Source",
                "Type",
                "Payload",
                "ReceivedAtUtc",
                "ProcessedAtUtc",
                "Status",
                "Error"
            FROM "InboxMessages"
            WHERE "Status" = @Status
            ORDER BY "ReceivedAtUtc" ASC
            LIMIT @Take
            FOR UPDATE SKIP LOCKED;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("Status", (int)InboxMessageStatus.Received);
        command.Parameters.AddWithValue("Take", take);

        return await ReadMessagesAsync(command);
    }

    /// <summary>
    /// Operação para buscar mensagens de inbox por filtros administrativos.
    /// </summary>
    /// <param name="messageId">Identificador idempotente opcional.</param>
    /// <param name="source">Sistema de origem opcional.</param>
    /// <param name="status">Status opcional da mensagem.</param>
    /// <param name="skip">Quantidade de registros ignorados.</param>
    /// <param name="take">Quantidade máxima de registros.</param>
    /// <returns>Coleção de mensagens encontradas.</returns>
    public async Task<IReadOnlyCollection<InboxMessage>> SearchAsync(
        Guid? messageId,
        string? source,
        InboxMessageStatus? status,
        int skip,
        int take)
    {
        const string sql = """
            SELECT
                "MessageId",
                "Source",
                "Type",
                "Payload",
                "ReceivedAtUtc",
                "ProcessedAtUtc",
                "Status",
                "Error"
            FROM "InboxMessages"
            WHERE (@MessageId IS NULL OR "MessageId" = @MessageId)
                AND (@Source IS NULL OR "Source" = @Source)
                AND (@Status IS NULL OR "Status" = @Status)
            ORDER BY "ReceivedAtUtc" DESC
            OFFSET @Skip
            LIMIT @Take;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("MessageId", messageId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("Source", string.IsNullOrWhiteSpace(source) ? DBNull.Value : source.Trim());
        command.Parameters.AddWithValue("Status", status.HasValue ? (int)status.Value : DBNull.Value);
        command.Parameters.AddWithValue("Skip", skip);
        command.Parameters.AddWithValue("Take", take);

        return await ReadMessagesAsync(command);
    }

    /// <summary>
    /// Operação para verificar se uma mensagem já foi recebida.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <returns>Verdadeiro quando a mensagem já existe.</returns>
    public async Task<bool> ExistsByMessageIdAsync(Guid messageId)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM "InboxMessages"
                WHERE "MessageId" = @MessageId
            );
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("MessageId", messageId);

        var exists = await command.ExecuteScalarAsync();

        return exists is true;
    }

    /// <summary>
    /// Operação para atualizar uma mensagem de inbox.
    /// </summary>
    /// <param name="message">Mensagem atualizada.</param>
    public async Task UpdateAsync(InboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        const string sql = """
            UPDATE "InboxMessages"
            SET
                "Source" = @Source,
                "Type" = @Type,
                "Payload" = @Payload,
                "ReceivedAtUtc" = @ReceivedAtUtc,
                "ProcessedAtUtc" = @ProcessedAtUtc,
                "Status" = @Status,
                "Error" = @Error
            WHERE "MessageId" = @MessageId;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        AddParameters(command, message);
        await command.ExecuteNonQueryAsync();
    }


    /// <summary>
    /// Operação para adicionar os parâmetros da inbox ao comando SQL.
    /// </summary>
    /// <param name="command">Comando SQL alvo.</param>
    /// <param name="message">Mensagem persistida.</param>
    private static void AddParameters(NpgsqlCommand command, InboxMessage message)
    {
        command.Parameters.AddWithValue("MessageId", message.MessageId);
        command.Parameters.AddWithValue("Source", message.Source);
        command.Parameters.AddWithValue("Type", message.Type);
        command.Parameters.AddWithValue("Payload", message.Payload);
        command.Parameters.AddWithValue("ReceivedAtUtc", message.ReceivedAtUtc);
        command.Parameters.AddWithValue("ProcessedAtUtc", message.ProcessedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("Status", (int)message.Status);
        command.Parameters.AddWithValue("Error", string.IsNullOrWhiteSpace(message.Error) ? DBNull.Value : SensitivePayloadSanitizer.SanitizeText(message.Error));
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

    /// <summary>
    /// Operação para materializar mensagens a partir do comando SQL.
    /// </summary>
    /// <param name="command">Comando SQL configurado.</param>
    /// <returns>Coleção de mensagens materializadas.</returns>
    private static async Task<IReadOnlyCollection<InboxMessage>> ReadMessagesAsync(NpgsqlCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync();
        var messages = new List<InboxMessage>();

        while (await reader.ReadAsync())
        {
            messages.Add(ReadMessage(reader));
        }

        return messages;
    }

    /// <summary>
    /// Operação para materializar uma mensagem a partir do leitor.
    /// </summary>
    /// <param name="reader">Leitor com os dados da mensagem.</param>
    /// <returns>Mensagem materializada ou nula.</returns>
    private static async Task<InboxMessage?> ReadMessageAsync(NpgsqlDataReader reader)
    {
        if (!await reader.ReadAsync())
            return null;

        return ReadMessage(reader);
    }

    /// <summary>
    /// Operação para materializar uma mensagem a partir da linha atual.
    /// </summary>
    /// <param name="reader">Leitor posicionado na mensagem.</param>
    /// <returns>Mensagem materializada.</returns>
    private static InboxMessage ReadMessage(NpgsqlDataReader reader)
    {
        return InboxMessage.Restore(
            reader.GetGuid(reader.GetOrdinal("MessageId")),
            reader.GetString(reader.GetOrdinal("Source")),
            reader.GetString(reader.GetOrdinal("Type")),
            reader.GetString(reader.GetOrdinal("Payload")),
            reader.GetDateTime(reader.GetOrdinal("ReceivedAtUtc")),
            reader.IsDBNull(reader.GetOrdinal("ProcessedAtUtc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("ProcessedAtUtc")),
            (InboxMessageStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            reader.IsDBNull(reader.GetOrdinal("Error"))
                ? null
                : reader.GetString(reader.GetOrdinal("Error")));
    }

}
