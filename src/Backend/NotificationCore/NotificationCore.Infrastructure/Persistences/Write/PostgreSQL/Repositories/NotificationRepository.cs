using BuildingBlocks.Messaging.Contracts.Security;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Entities;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Repositories;
using NotificationCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace NotificationCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de notificações.
/// </summary>
internal sealed class NotificationRepository : INotificationRepository
{
    /// <summary>
    /// Campo que armazena database session.
    /// </summary>
    private readonly IDatabaseSession _databaseSession;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public NotificationRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }


    /// <summary>
    /// Operação para adicionar uma notificação.
    /// </summary>
    /// <param name="notification">Notificação a ser persistida.</param>
    public async Task AddAsync(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await InsertNotificationAsync(connection, notification);
        await UpsertDeliveryAttemptsAsync(connection, notification.DeliveryAttempts);
    }

    /// <summary>
    /// Operação para tentar adicionar uma notificação de forma idempotente.
    /// </summary>
    /// <param name="notification">Notificação a ser persistida.</param>
    /// <returns>Verdadeiro quando a notificação foi adicionada.</returns>
    public async Task<bool> TryAddAsync(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        const string sql = """
            INSERT INTO "Notifications"
            (
                "Id",
                "Source",
                "CorrelationId",
                "IdempotencyKey",
                "Channel",
                "Recipient",
                "TemplateKey",
                "Status",
                "Priority",
                "RequestedAtUtc",
                "ScheduledAtUtc",
                "CreatedAtUtc",
                "SentAtUtc",
                "FailedAtUtc",
                "LastError"
            )
            VALUES
            (
                @Id,
                @Source,
                @CorrelationId,
                @IdempotencyKey,
                @Channel,
                @Recipient,
                @TemplateKey,
                @Status,
                @Priority,
                @RequestedAtUtc,
                @ScheduledAtUtc,
                @CreatedAtUtc,
                @SentAtUtc,
                @FailedAtUtc,
                @LastError
            )
            ON CONFLICT ("IdempotencyKey") DO NOTHING;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        AddNotificationParameters(command, notification);

        var wasInserted = await command.ExecuteNonQueryAsync() > 0;

        if (wasInserted)
            await UpsertDeliveryAttemptsAsync(connection, notification.DeliveryAttempts);

        return wasInserted;
    }

    /// <summary>
    /// Operação para obter uma notificação pelo identificador.
    /// </summary>
    /// <param name="notificationId">Identificador da notificação.</param>
    /// <returns>Notificação encontrada ou nula.</returns>
    public async Task<Notification?> GetByIdAsync(Guid notificationId)
    {
        const string sql = """
            SELECT
                "Id",
                "Source",
                "CorrelationId",
                "IdempotencyKey",
                "Channel",
                "Recipient",
                "TemplateKey",
                "Status",
                "Priority",
                "RequestedAtUtc",
                "ScheduledAtUtc",
                "CreatedAtUtc",
                "SentAtUtc",
                "FailedAtUtc",
                "LastError"
            FROM "Notifications"
            WHERE "Id" = @NotificationId
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("NotificationId", notificationId);

        var notification = await ReadNotificationAsync(command);

        if (notification is null)
            return null;

        var attempts = await GetDeliveryAttemptsAsync(connection, [notification.Id]);

        return RestoreNotification(notification, attempts);
    }

    /// <summary>
    /// Operação para obter uma notificação pela chave de idempotência.
    /// </summary>
    /// <param name="idempotencyKey">Chave de idempotência da notificação.</param>
    /// <returns>Notificação encontrada ou nula.</returns>
    public async Task<Notification?> GetByIdempotencyKeyAsync(string idempotencyKey)
    {
        const string sql = """
            SELECT
                "Id",
                "Source",
                "CorrelationId",
                "IdempotencyKey",
                "Channel",
                "Recipient",
                "TemplateKey",
                "Status",
                "Priority",
                "RequestedAtUtc",
                "ScheduledAtUtc",
                "CreatedAtUtc",
                "SentAtUtc",
                "FailedAtUtc",
                "LastError"
            FROM "Notifications"
            WHERE "IdempotencyKey" = @IdempotencyKey
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("IdempotencyKey", idempotencyKey.Trim());

        var notification = await ReadNotificationAsync(command);

        if (notification is null)
            return null;

        var attempts = await GetDeliveryAttemptsAsync(connection, [notification.Id]);

        return RestoreNotification(notification, attempts);
    }

    /// <summary>
    /// Operação para obter notificações pendentes ou com retry liberado.
    /// </summary>
    /// <param name="dueAtUtc">Data limite de agendamento em UTC.</param>
    /// <param name="take">Quantidade máxima de notificações.</param>
    /// <returns>Coleção de notificações disponíveis para processamento.</returns>
    public async Task<IReadOnlyCollection<Notification>> GetPendingForDispatchAsync(DateTime dueAtUtc, int take)
    {
        const string sql = """
            SELECT
                "Id",
                "Source",
                "CorrelationId",
                "IdempotencyKey",
                "Channel",
                "Recipient",
                "TemplateKey",
                "Status",
                "Priority",
                "RequestedAtUtc",
                "ScheduledAtUtc",
                "CreatedAtUtc",
                "SentAtUtc",
                "FailedAtUtc",
                "LastError"
            FROM "Notifications"
            WHERE "Status" IN (@PendingStatus, @RetryScheduledStatus)
                AND "ScheduledAtUtc" <= @DueAtUtc
            ORDER BY "Priority" DESC, "ScheduledAtUtc" ASC, "CreatedAtUtc" ASC
            LIMIT @Take
            FOR UPDATE SKIP LOCKED;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("PendingStatus", (int)NotificationStatus.Pending);
        command.Parameters.AddWithValue("RetryScheduledStatus", (int)NotificationStatus.RetryScheduled);
        command.Parameters.AddWithValue("DueAtUtc", dueAtUtc);
        command.Parameters.AddWithValue("Take", take);

        return await ReadNotificationsWithAttemptsAsync(connection, command);
    }

    /// <summary>
    /// Operação para obter notificações em processamento com lease expirado.
    /// </summary>
    /// <param name="dueAtUtc">Data limite de expiração em UTC.</param>
    /// <param name="take">Quantidade máxima de notificações.</param>
    /// <returns>Coleção de notificações com processamento expirado.</returns>
    public async Task<IReadOnlyCollection<Notification>> GetProcessingTimedOutAsync(DateTime dueAtUtc, int take)
    {
        const string sql = """
            SELECT
                "Id",
                "Source",
                "CorrelationId",
                "IdempotencyKey",
                "Channel",
                "Recipient",
                "TemplateKey",
                "Status",
                "Priority",
                "RequestedAtUtc",
                "ScheduledAtUtc",
                "CreatedAtUtc",
                "SentAtUtc",
                "FailedAtUtc",
                "LastError"
            FROM "Notifications"
            WHERE "Status" = @ProcessingStatus
                AND "ScheduledAtUtc" <= @DueAtUtc
            ORDER BY "ScheduledAtUtc" ASC, "CreatedAtUtc" ASC
            LIMIT @Take
            FOR UPDATE SKIP LOCKED;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("ProcessingStatus", (int)NotificationStatus.Processing);
        command.Parameters.AddWithValue("DueAtUtc", dueAtUtc);
        command.Parameters.AddWithValue("Take", take);

        return await ReadNotificationsWithAttemptsAsync(connection, command);
    }

    /// <summary>
    /// Operação para buscar notificações por filtros administrativos.
    /// </summary>
    /// <param name="correlationId">Identificador de correlação opcional.</param>
    /// <param name="status">Status opcional da notificação.</param>
    /// <param name="skip">Quantidade de registros ignorados.</param>
    /// <param name="take">Quantidade máxima de registros.</param>
    /// <returns>Coleção de notificações encontradas.</returns>
    public async Task<IReadOnlyCollection<Notification>> SearchAsync(
        string? correlationId,
        NotificationStatus? status,
        int skip,
        int take)
    {
        const string sql = """
            SELECT
                "Id",
                "Source",
                "CorrelationId",
                "IdempotencyKey",
                "Channel",
                "Recipient",
                "TemplateKey",
                "Status",
                "Priority",
                "RequestedAtUtc",
                "ScheduledAtUtc",
                "CreatedAtUtc",
                "SentAtUtc",
                "FailedAtUtc",
                "LastError"
            FROM "Notifications"
            WHERE (@CorrelationId IS NULL OR "CorrelationId" = @CorrelationId)
                AND (@Status IS NULL OR "Status" = @Status)
            ORDER BY "CreatedAtUtc" DESC
            OFFSET @Skip
            LIMIT @Take;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("CorrelationId", string.IsNullOrWhiteSpace(correlationId) ? DBNull.Value : correlationId.Trim());
        command.Parameters.AddWithValue("Status", status.HasValue ? (int)status.Value : DBNull.Value);
        command.Parameters.AddWithValue("Skip", skip);
        command.Parameters.AddWithValue("Take", take);

        return await ReadNotificationsWithAttemptsAsync(connection, command);
    }

    /// <summary>
    /// Operação para atualizar uma notificação.
    /// </summary>
    /// <param name="notification">Notificação atualizada.</param>
    public async Task UpdateAsync(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await UpdateNotificationAsync(connection, notification);
        await UpsertDeliveryAttemptsAsync(connection, notification.DeliveryAttempts);
    }

    /// <summary>
    /// Operação para tentar atualizar notificação com processamento expirado.
    /// </summary>
    /// <param name="notification">Notificação atualizada.</param>
    /// <param name="processingTimeoutAtUtc">Data de expiração esperada do processamento.</param>
    /// <returns>Verdadeiro quando a notificação foi atualizada.</returns>
    public async Task<bool> TryUpdateProcessingTimedOutAsync(
        Notification notification,
        DateTime processingTimeoutAtUtc)
    {
        ArgumentNullException.ThrowIfNull(notification);

        const string sql = """
            UPDATE "Notifications"
            SET
                "Source" = @Source,
                "CorrelationId" = @CorrelationId,
                "IdempotencyKey" = @IdempotencyKey,
                "Channel" = @Channel,
                "Recipient" = @Recipient,
                "TemplateKey" = @TemplateKey,
                "Status" = @Status,
                "Priority" = @Priority,
                "RequestedAtUtc" = @RequestedAtUtc,
                "ScheduledAtUtc" = @ScheduledAtUtc,
                "CreatedAtUtc" = @CreatedAtUtc,
                "SentAtUtc" = @SentAtUtc,
                "FailedAtUtc" = @FailedAtUtc,
                "LastError" = @LastError
            WHERE "Id" = @Id
                AND "Status" = @ExpectedStatus
                AND "ScheduledAtUtc" = @ProcessingTimeoutAtUtc;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        AddNotificationParameters(command, notification);
        command.Parameters.AddWithValue("ExpectedStatus", (int)NotificationStatus.Processing);
        command.Parameters.AddWithValue("ProcessingTimeoutAtUtc", processingTimeoutAtUtc);

        var wasUpdated = await command.ExecuteNonQueryAsync() > 0;

        if (wasUpdated)
            await UpsertDeliveryAttemptsAsync(connection, notification.DeliveryAttempts);

        return wasUpdated;
    }


    /// <summary>
    /// Operação para inserir uma notificação.
    /// </summary>
    /// <param name="connection">Conexão aberta da sessão.</param>
    /// <param name="notification">Notificação a persistir.</param>
    private async Task InsertNotificationAsync(NpgsqlConnection connection, Notification notification)
    {
        const string sql = """
            INSERT INTO "Notifications"
            (
                "Id",
                "Source",
                "CorrelationId",
                "IdempotencyKey",
                "Channel",
                "Recipient",
                "TemplateKey",
                "Status",
                "Priority",
                "RequestedAtUtc",
                "ScheduledAtUtc",
                "CreatedAtUtc",
                "SentAtUtc",
                "FailedAtUtc",
                "LastError"
            )
            VALUES
            (
                @Id,
                @Source,
                @CorrelationId,
                @IdempotencyKey,
                @Channel,
                @Recipient,
                @TemplateKey,
                @Status,
                @Priority,
                @RequestedAtUtc,
                @ScheduledAtUtc,
                @CreatedAtUtc,
                @SentAtUtc,
                @FailedAtUtc,
                @LastError
            );
            """;

        await using var command = CreateCommand(connection, sql);
        AddNotificationParameters(command, notification);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para atualizar uma notificação.
    /// </summary>
    /// <param name="connection">Conexão aberta da sessão.</param>
    /// <param name="notification">Notificação atualizada.</param>
    private async Task UpdateNotificationAsync(NpgsqlConnection connection, Notification notification)
    {
        const string sql = """
            UPDATE "Notifications"
            SET
                "Source" = @Source,
                "CorrelationId" = @CorrelationId,
                "IdempotencyKey" = @IdempotencyKey,
                "Channel" = @Channel,
                "Recipient" = @Recipient,
                "TemplateKey" = @TemplateKey,
                "Status" = @Status,
                "Priority" = @Priority,
                "RequestedAtUtc" = @RequestedAtUtc,
                "ScheduledAtUtc" = @ScheduledAtUtc,
                "CreatedAtUtc" = @CreatedAtUtc,
                "SentAtUtc" = @SentAtUtc,
                "FailedAtUtc" = @FailedAtUtc,
                "LastError" = @LastError
            WHERE "Id" = @Id;
            """;

        await using var command = CreateCommand(connection, sql);
        AddNotificationParameters(command, notification);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para inserir ou atualizar tentativas de entrega.
    /// </summary>
    /// <param name="connection">Conexão aberta da sessão.</param>
    /// <param name="deliveryAttempts">Tentativas a persistir.</param>
    private async Task UpsertDeliveryAttemptsAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<DeliveryAttempt> deliveryAttempts)
    {
        const string sql = """
            INSERT INTO "NotificationDeliveryAttempts"
            (
                "Id",
                "NotificationId",
                "Provider",
                "Status",
                "AttemptNumber",
                "StartedAtUtc",
                "FinishedAtUtc",
                "ErrorCode",
                "ErrorMessage",
                "ProviderMessageId"
            )
            VALUES
            (
                @Id,
                @NotificationId,
                @Provider,
                @Status,
                @AttemptNumber,
                @StartedAtUtc,
                @FinishedAtUtc,
                @ErrorCode,
                @ErrorMessage,
                @ProviderMessageId
            )
            ON CONFLICT ("Id") DO UPDATE
            SET
                "NotificationId" = EXCLUDED."NotificationId",
                "Provider" = EXCLUDED."Provider",
                "Status" = EXCLUDED."Status",
                "AttemptNumber" = EXCLUDED."AttemptNumber",
                "StartedAtUtc" = EXCLUDED."StartedAtUtc",
                "FinishedAtUtc" = EXCLUDED."FinishedAtUtc",
                "ErrorCode" = EXCLUDED."ErrorCode",
                "ErrorMessage" = EXCLUDED."ErrorMessage",
                "ProviderMessageId" = EXCLUDED."ProviderMessageId";
            """;

        foreach (var deliveryAttempt in deliveryAttempts)
        {
            await using var command = CreateCommand(connection, sql);
            AddDeliveryAttemptParameters(command, deliveryAttempt);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Operação para buscar tentativas de entrega por notificações.
    /// </summary>
    /// <param name="connection">Conexão aberta da sessão.</param>
    /// <param name="notificationIds">Identificadores das notificações.</param>
    /// <returns>Tentativas agrupadas por notificação.</returns>
    private async Task<Dictionary<Guid, List<DeliveryAttempt>>> GetDeliveryAttemptsAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<Guid> notificationIds)
    {
        if (notificationIds.Count == 0)
            return [];

        const string sql = """
            SELECT
                "Id",
                "NotificationId",
                "Provider",
                "Status",
                "AttemptNumber",
                "StartedAtUtc",
                "FinishedAtUtc",
                "ErrorCode",
                "ErrorMessage",
                "ProviderMessageId"
            FROM "NotificationDeliveryAttempts"
            WHERE "NotificationId" = ANY(@NotificationIds)
            ORDER BY "NotificationId" ASC, "AttemptNumber" ASC;
            """;

        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("NotificationIds", notificationIds.ToArray());

        await using var reader = await command.ExecuteReaderAsync();
        var attempts = new Dictionary<Guid, List<DeliveryAttempt>>();

        while (await reader.ReadAsync())
        {
            var deliveryAttempt = ReadDeliveryAttempt(reader);

            if (!attempts.TryGetValue(deliveryAttempt.NotificationId, out var notificationAttempts))
            {
                notificationAttempts = [];
                attempts.Add(deliveryAttempt.NotificationId, notificationAttempts);
            }

            notificationAttempts.Add(deliveryAttempt);
        }

        return attempts;
    }

    /// <summary>
    /// Operação para adicionar parâmetros da notificação ao comando SQL.
    /// </summary>
    /// <param name="command">Comando SQL alvo.</param>
    /// <param name="notification">Notificação persistida.</param>
    private static void AddNotificationParameters(NpgsqlCommand command, Notification notification)
    {
        command.Parameters.AddWithValue("Id", notification.Id);
        command.Parameters.AddWithValue("Source", notification.Source);
        command.Parameters.AddWithValue("CorrelationId", notification.CorrelationId);
        command.Parameters.AddWithValue("IdempotencyKey", notification.IdempotencyKey.Value);
        command.Parameters.AddWithValue("Channel", (int)notification.Channel);
        command.Parameters.AddWithValue("Recipient", notification.Recipient.Value);
        command.Parameters.AddWithValue("TemplateKey", notification.TemplateKey.Value);
        command.Parameters.AddWithValue("Status", (int)notification.Status);
        command.Parameters.AddWithValue("Priority", (int)notification.Priority);
        command.Parameters.AddWithValue("RequestedAtUtc", notification.RequestedAtUtc);
        command.Parameters.AddWithValue("ScheduledAtUtc", notification.ScheduledAtUtc);
        command.Parameters.AddWithValue("CreatedAtUtc", notification.CreatedAtUtc);
        command.Parameters.AddWithValue("SentAtUtc", notification.SentAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("FailedAtUtc", notification.FailedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("LastError", string.IsNullOrWhiteSpace(notification.LastError) ? DBNull.Value : SensitivePayloadSanitizer.SanitizeText(notification.LastError));
    }

    /// <summary>
    /// Operação para adicionar parâmetros da tentativa de entrega ao comando SQL.
    /// </summary>
    /// <param name="command">Comando SQL alvo.</param>
    /// <param name="deliveryAttempt">Tentativa persistida.</param>
    private static void AddDeliveryAttemptParameters(NpgsqlCommand command, DeliveryAttempt deliveryAttempt)
    {
        command.Parameters.AddWithValue("Id", deliveryAttempt.Id);
        command.Parameters.AddWithValue("NotificationId", deliveryAttempt.NotificationId);
        command.Parameters.AddWithValue("Provider", deliveryAttempt.Provider);
        command.Parameters.AddWithValue("Status", (int)deliveryAttempt.Status);
        command.Parameters.AddWithValue("AttemptNumber", deliveryAttempt.AttemptNumber);
        command.Parameters.AddWithValue("StartedAtUtc", deliveryAttempt.StartedAtUtc);
        command.Parameters.AddWithValue("FinishedAtUtc", deliveryAttempt.FinishedAtUtc);
        command.Parameters.AddWithValue("ErrorCode", string.IsNullOrWhiteSpace(deliveryAttempt.ErrorCode) ? DBNull.Value : deliveryAttempt.ErrorCode);
        command.Parameters.AddWithValue("ErrorMessage", string.IsNullOrWhiteSpace(deliveryAttempt.ErrorMessage) ? DBNull.Value : SensitivePayloadSanitizer.SanitizeText(deliveryAttempt.ErrorMessage));
        command.Parameters.AddWithValue("ProviderMessageId", string.IsNullOrWhiteSpace(deliveryAttempt.ProviderMessageId) ? DBNull.Value : deliveryAttempt.ProviderMessageId);
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
    /// Operação para materializar notificações com tentativas.
    /// </summary>
    /// <param name="connection">Conexão aberta da sessão.</param>
    /// <param name="command">Comando SQL configurado.</param>
    /// <returns>Coleção de notificações materializadas.</returns>
    private async Task<IReadOnlyCollection<Notification>> ReadNotificationsWithAttemptsAsync(
        NpgsqlConnection connection,
        NpgsqlCommand command)
    {
        var notifications = await ReadNotificationsAsync(command);
        var attempts = await GetDeliveryAttemptsAsync(connection, notifications.Select(notification => notification.Id).ToArray());

        return notifications
            .Select(notification => RestoreNotification(notification, attempts))
            .ToList();
    }

    /// <summary>
    /// Operação para materializar notificações a partir do comando SQL.
    /// </summary>
    /// <param name="command">Comando SQL configurado.</param>
    /// <returns>Coleção de notificações sem tentativas.</returns>
    private static async Task<IReadOnlyCollection<Notification>> ReadNotificationsAsync(NpgsqlCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync();
        var notifications = new List<Notification>();

        while (await reader.ReadAsync())
        {
            notifications.Add(ReadNotification(reader));
        }

        return notifications;
    }

    /// <summary>
    /// Operação para materializar uma notificação a partir do comando SQL.
    /// </summary>
    /// <param name="command">Comando SQL configurado.</param>
    /// <returns>Notificação materializada ou nula.</returns>
    private static async Task<Notification?> ReadNotificationAsync(NpgsqlCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return ReadNotification(reader);
    }

    /// <summary>
    /// Operação para materializar uma notificação a partir da linha atual.
    /// </summary>
    /// <param name="reader">Leitor posicionado na notificação.</param>
    /// <returns>Notificação materializada sem tentativas.</returns>
    private static Notification ReadNotification(NpgsqlDataReader reader)
    {
        return Notification.Restore(
            id: reader.GetGuid(reader.GetOrdinal("Id")),
            source: reader.GetString(reader.GetOrdinal("Source")),
            correlationId: reader.GetString(reader.GetOrdinal("CorrelationId")),
            idempotencyKey: reader.GetString(reader.GetOrdinal("IdempotencyKey")),
            channel: (NotificationChannel)reader.GetInt32(reader.GetOrdinal("Channel")),
            recipient: reader.GetString(reader.GetOrdinal("Recipient")),
            templateKey: reader.GetString(reader.GetOrdinal("TemplateKey")),
            status: (NotificationStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            priority: (NotificationPriority)reader.GetInt32(reader.GetOrdinal("Priority")),
            requestedAtUtc: reader.GetDateTime(reader.GetOrdinal("RequestedAtUtc")),
            scheduledAtUtc: reader.GetDateTime(reader.GetOrdinal("ScheduledAtUtc")),
            createdAtUtc: reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
            sentAtUtc: reader.IsDBNull(reader.GetOrdinal("SentAtUtc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("SentAtUtc")),
            failedAtUtc: reader.IsDBNull(reader.GetOrdinal("FailedAtUtc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("FailedAtUtc")),
            lastError: reader.IsDBNull(reader.GetOrdinal("LastError"))
                ? null
                : reader.GetString(reader.GetOrdinal("LastError")));
    }

    /// <summary>
    /// Operação para materializar uma tentativa de entrega a partir da linha atual.
    /// </summary>
    /// <param name="reader">Leitor posicionado na tentativa.</param>
    /// <returns>Tentativa materializada.</returns>
    private static DeliveryAttempt ReadDeliveryAttempt(NpgsqlDataReader reader)
    {
        return DeliveryAttempt.Restore(
            id: reader.GetGuid(reader.GetOrdinal("Id")),
            notificationId: reader.GetGuid(reader.GetOrdinal("NotificationId")),
            provider: reader.GetString(reader.GetOrdinal("Provider")),
            status: (DeliveryAttemptStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            attemptNumber: reader.GetInt32(reader.GetOrdinal("AttemptNumber")),
            startedAtUtc: reader.GetDateTime(reader.GetOrdinal("StartedAtUtc")),
            finishedAtUtc: reader.GetDateTime(reader.GetOrdinal("FinishedAtUtc")),
            errorCode: reader.IsDBNull(reader.GetOrdinal("ErrorCode"))
                ? null
                : reader.GetString(reader.GetOrdinal("ErrorCode")),
            errorMessage: reader.IsDBNull(reader.GetOrdinal("ErrorMessage"))
                ? null
                : reader.GetString(reader.GetOrdinal("ErrorMessage")),
            providerMessageId: reader.IsDBNull(reader.GetOrdinal("ProviderMessageId"))
                ? null
                : reader.GetString(reader.GetOrdinal("ProviderMessageId")));
    }

    /// <summary>
    /// Operação para restaurar uma notificação com suas tentativas.
    /// </summary>
    /// <param name="notification">Notificação sem tentativas.</param>
    /// <param name="attempts">Tentativas agrupadas por notificação.</param>
    /// <returns>Notificação restaurada com tentativas.</returns>
    private static Notification RestoreNotification(
        Notification notification,
        IReadOnlyDictionary<Guid, List<DeliveryAttempt>> attempts)
    {
        attempts.TryGetValue(notification.Id, out var notificationAttempts);

        return Notification.Restore(
            id: notification.Id,
            source: notification.Source,
            correlationId: notification.CorrelationId,
            idempotencyKey: notification.IdempotencyKey.Value,
            channel: notification.Channel,
            recipient: notification.Recipient.Value,
            templateKey: notification.TemplateKey.Value,
            status: notification.Status,
            priority: notification.Priority,
            requestedAtUtc: notification.RequestedAtUtc,
            scheduledAtUtc: notification.ScheduledAtUtc,
            createdAtUtc: notification.CreatedAtUtc,
            deliveryAttempts: notificationAttempts,
            sentAtUtc: notification.SentAtUtc,
            failedAtUtc: notification.FailedAtUtc,
            lastError: notification.LastError);
    }

}
