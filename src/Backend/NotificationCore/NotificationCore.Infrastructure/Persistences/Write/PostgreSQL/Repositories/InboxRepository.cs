using Shared.Messaging.Contracts.Security;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositorio PostgreSQL da inbox.
/// </summary>
internal sealed class InboxRepository : IInboxRepository
{
    private const string DEFAULT_SOURCE = "Unknown";
    private const int STATUS_RECEIVED = 1;
    private const int STATUS_PROCESSED = 2;
    private const int STATUS_FAILED = 3;
    private const int STATUS_PROCESSING = 4;

    /// <summary>
    /// Campo que armazena database session.
    /// </summary>
    private readonly IDatabaseSession _databaseSession;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="databaseSession">Sessao atual de banco de dados.</param>
    public InboxRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }


    /// <summary>
    /// Operacao para tentar iniciar o processamento idempotente da mensagem.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="messageType">Tipo logico da mensagem.</param>
    /// <param name="consumerName">Nome do consumidor.</param>
    /// <param name="payload">Conteudo serializado da mensagem.</param>
    /// <param name="receivedAtUtc">Data de recebimento em UTC.</param>
    /// <returns>Resultado da tentativa de inicio.</returns>
    public async Task<InboxProcessingStartResult> TryStartProcessingAsync(
        Guid messageId,
        string messageType,
        string consumerName,
        string payload,
        DateTime receivedAtUtc)
    {
        const string insertSql = """
            INSERT INTO "InboxMessages"
            (
                "Id",
                "MessageId",
                "Source",
                "MessageType",
                "ConsumerName",
                "Payload",
                "ReceivedAtUtc",
                "Status",
                "RetryCount",
                "CreatedAtUtc",
                "UpdatedAtUtc"
            )
            VALUES
            (
                @Id,
                @MessageId,
                @Source,
                @MessageType,
                @ConsumerName,
                @Payload,
                @ReceivedAtUtc,
                @Status,
                0,
                @ReceivedAtUtc,
                @ReceivedAtUtc
            )
            ON CONFLICT ("MessageId", "MessageType", "ConsumerName") DO NOTHING;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var insertCommand = CreateCommand(connection, insertSql);
        AddIdentityParameters(insertCommand, messageId, messageType, consumerName);
        insertCommand.Parameters.AddWithValue("Id", Guid.NewGuid());
        insertCommand.Parameters.AddWithValue("Source", DEFAULT_SOURCE);
        insertCommand.Parameters.AddWithValue("Payload", payload);
        insertCommand.Parameters.AddWithValue("ReceivedAtUtc", receivedAtUtc);
        insertCommand.Parameters.AddWithValue("Status", STATUS_PROCESSING);

        if (await insertCommand.ExecuteNonQueryAsync() > 0)
            return InboxProcessingStartResult.Started(retryCount: 0);

        return await TryRestartFailedAsync(
            connection,
            messageId,
            messageType,
            consumerName,
            payload,
            receivedAtUtc);
    }

    /// <summary>
    /// Operacao para obter o payload original pela chave de idempotencia da notificacao.
    /// </summary>
    /// <param name="idempotencyKey">Chave de idempotencia da notificacao.</param>
    /// <returns>Payload encontrado ou nulo.</returns>
    public async Task<string?> GetPayloadByNotificationIdempotencyKeyAsync(string idempotencyKey)
    {
        const string sql = """
            SELECT "Payload"
            FROM "InboxMessages"
            WHERE "Payload"::jsonb ->> 'IdempotencyKey' = @IdempotencyKey
            ORDER BY "ReceivedAtUtc" ASC
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("IdempotencyKey", idempotencyKey.Trim());

        var payload = await command.ExecuteScalarAsync();

        return payload as string;
    }

    /// <summary>
    /// Operacao para marcar mensagem como processada.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="messageType">Tipo logico da mensagem.</param>
    /// <param name="consumerName">Nome do consumidor.</param>
    /// <param name="processedAtUtc">Data de processamento em UTC.</param>
    /// <returns>Tarefa concluida apos atualizar a mensagem.</returns>
    public async Task MarkAsProcessedAsync(
        Guid messageId,
        string messageType,
        string consumerName,
        DateTime processedAtUtc)
    {
        const string sql = """
            UPDATE "InboxMessages"
            SET
                "Status" = @Status,
                "ProcessedAtUtc" = @ProcessedAtUtc,
                "Error" = NULL,
                "UpdatedAtUtc" = @ProcessedAtUtc
            WHERE "MessageId" = @MessageId
                AND "MessageType" = @MessageType
                AND "ConsumerName" = @ConsumerName;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        AddIdentityParameters(command, messageId, messageType, consumerName);
        command.Parameters.AddWithValue("Status", STATUS_PROCESSED);
        command.Parameters.AddWithValue("ProcessedAtUtc", processedAtUtc);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operacao para marcar mensagem como falha.
    /// </summary>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="messageType">Tipo logico da mensagem.</param>
    /// <param name="consumerName">Nome do consumidor.</param>
    /// <param name="payload">Conteudo serializado da mensagem.</param>
    /// <param name="receivedAtUtc">Data de recebimento em UTC.</param>
    /// <param name="error">Erro sanitizado da tentativa.</param>
    /// <returns>Tarefa concluida apos registrar a falha.</returns>
    public async Task MarkAsFailedAsync(
        Guid messageId,
        string messageType,
        string consumerName,
        string payload,
        DateTime receivedAtUtc,
        string error)
    {
        const string sql = """
            INSERT INTO "InboxMessages"
            (
                "Id",
                "MessageId",
                "Source",
                "MessageType",
                "ConsumerName",
                "Payload",
                "ReceivedAtUtc",
                "Status",
                "Error",
                "RetryCount",
                "CreatedAtUtc",
                "UpdatedAtUtc"
            )
            VALUES
            (
                @Id,
                @MessageId,
                @Source,
                @MessageType,
                @ConsumerName,
                @Payload,
                @ReceivedAtUtc,
                @Status,
                @Error,
                1,
                @ReceivedAtUtc,
                @UpdatedAtUtc
            )
            ON CONFLICT ("MessageId", "MessageType", "ConsumerName") DO UPDATE
            SET
                "Status" = EXCLUDED."Status",
                "Error" = EXCLUDED."Error",
                "RetryCount" = "InboxMessages"."RetryCount" + 1,
                "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
            WHERE "InboxMessages"."Status" <> @ProcessedStatus;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        AddIdentityParameters(command, messageId, messageType, consumerName);
        command.Parameters.AddWithValue("Id", Guid.NewGuid());
        command.Parameters.AddWithValue("Source", DEFAULT_SOURCE);
        command.Parameters.AddWithValue("Payload", payload);
        command.Parameters.AddWithValue("ReceivedAtUtc", receivedAtUtc);
        command.Parameters.AddWithValue("UpdatedAtUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("Status", STATUS_FAILED);
        command.Parameters.AddWithValue("ProcessedStatus", STATUS_PROCESSED);
        command.Parameters.AddWithValue("Error", SensitivePayloadSanitizer.SanitizeText(error));

        await command.ExecuteNonQueryAsync();
    }


    /// <summary>
    /// Operacao para tentar reiniciar uma mensagem com falha.
    /// </summary>
    /// <param name="connection">Conexao aberta da sessao.</param>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="messageType">Tipo logico da mensagem.</param>
    /// <param name="consumerName">Nome do consumidor.</param>
    /// <param name="payload">Conteudo serializado da mensagem.</param>
    /// <param name="receivedAtUtc">Data de recebimento em UTC.</param>
    /// <returns>Resultado da tentativa de reinicio.</returns>
    private async Task<InboxProcessingStartResult> TryRestartFailedAsync(
        NpgsqlConnection connection,
        Guid messageId,
        string messageType,
        string consumerName,
        string payload,
        DateTime receivedAtUtc)
    {
        const string updateSql = """
            UPDATE "InboxMessages"
            SET
                "Status" = @ProcessingStatus,
                "Payload" = @Payload,
                "ReceivedAtUtc" = @ReceivedAtUtc,
                "ProcessedAtUtc" = NULL,
                "Error" = NULL,
                "RetryCount" = "RetryCount" + 1,
                "UpdatedAtUtc" = @ReceivedAtUtc
            WHERE "MessageId" = @MessageId
                AND "MessageType" = @MessageType
                AND "ConsumerName" = @ConsumerName
                AND "Status" IN (@ReceivedStatus, @FailedStatus)
            RETURNING "RetryCount";
            """;

        await using var updateCommand = CreateCommand(connection, updateSql);
        AddIdentityParameters(updateCommand, messageId, messageType, consumerName);
        updateCommand.Parameters.AddWithValue("Payload", payload);
        updateCommand.Parameters.AddWithValue("ReceivedAtUtc", receivedAtUtc);
        updateCommand.Parameters.AddWithValue("ProcessingStatus", STATUS_PROCESSING);
        updateCommand.Parameters.AddWithValue("ReceivedStatus", STATUS_RECEIVED);
        updateCommand.Parameters.AddWithValue("FailedStatus", STATUS_FAILED);

        var retryCount = await updateCommand.ExecuteScalarAsync();

        if (retryCount is int count)
            return InboxProcessingStartResult.Started(count);

        return await GetSkippedResultAsync(connection, messageId, messageType, consumerName);
    }

    /// <summary>
    /// Operacao para obter o resultado quando outra instancia ja registrou a mensagem.
    /// </summary>
    /// <param name="connection">Conexao aberta da sessao.</param>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="messageType">Tipo logico da mensagem.</param>
    /// <param name="consumerName">Nome do consumidor.</param>
    /// <returns>Resultado de mensagem ignorada.</returns>
    private async Task<InboxProcessingStartResult> GetSkippedResultAsync(
        NpgsqlConnection connection,
        Guid messageId,
        string messageType,
        string consumerName)
    {
        const string sql = """
            SELECT "Status", "RetryCount"
            FROM "InboxMessages"
            WHERE "MessageId" = @MessageId
                AND "MessageType" = @MessageType
                AND "ConsumerName" = @ConsumerName
            LIMIT 1;
            """;

        await using var command = CreateCommand(connection, sql);
        AddIdentityParameters(command, messageId, messageType, consumerName);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return InboxProcessingStartResult.Skipped(wasAlreadyProcessed: false, retryCount: 0);

        var status = reader.GetInt32(reader.GetOrdinal("Status"));
        var retryCount = reader.GetInt32(reader.GetOrdinal("RetryCount"));

        return InboxProcessingStartResult.Skipped(
            wasAlreadyProcessed: status == STATUS_PROCESSED,
            retryCount);
    }

    /// <summary>
    /// Operacao para adicionar parametros de identidade da inbox.
    /// </summary>
    /// <param name="command">Comando SQL alvo.</param>
    /// <param name="messageId">Identificador idempotente da mensagem.</param>
    /// <param name="messageType">Tipo logico da mensagem.</param>
    /// <param name="consumerName">Nome do consumidor.</param>
    private static void AddIdentityParameters(
        NpgsqlCommand command,
        Guid messageId,
        string messageType,
        string consumerName)
    {
        command.Parameters.AddWithValue("MessageId", messageId);
        command.Parameters.AddWithValue("MessageType", messageType.Trim());
        command.Parameters.AddWithValue("ConsumerName", consumerName.Trim());
    }

    /// <summary>
    /// Operacao para criar comando SQL respeitando a transacao atual.
    /// </summary>
    /// <param name="connection">Conexao aberta da sessao.</param>
    /// <param name="sql">Comando SQL a ser executado.</param>
    /// <returns>Comando pronto para uso.</returns>
    private NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql)
    {
        return new NpgsqlCommand(sql, connection, _databaseSession.CurrentTransaction);
    }
}
