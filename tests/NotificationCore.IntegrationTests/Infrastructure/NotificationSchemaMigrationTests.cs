using Npgsql;

namespace NotificationCore.IntegrationTests.Infrastructure;

public sealed class NotificationSchemaMigrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    /// <summary>
    /// Campo que armazena fixture.
    /// </summary>
    private readonly PostgreSqlIntegrationFixture _fixture;

    public NotificationSchemaMigrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migration_WhenApplied_ShouldCreateNotificationSchema()
    {
        if (!_fixture.IsAvailable)
            return;

        await AssertColumnsAsync(
            "InboxMessages",
            "MessageId",
            "Source",
            "Type",
            "Payload",
            "ReceivedAtUtc",
            "ProcessedAtUtc",
            "Status",
            "Error");

        await AssertColumnsAsync(
            "Notifications",
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
            "LastError");

        await AssertColumnsAsync(
            "NotificationDeliveryAttempts",
            "Id",
            "NotificationId",
            "Provider",
            "Status",
            "AttemptNumber",
            "StartedAtUtc",
            "FinishedAtUtc",
            "ErrorCode",
            "ErrorMessage",
            "ProviderMessageId");

        await AssertColumnsAsync(
            "NotificationTemplates",
            "Id",
            "TemplateKey",
            "Channel",
            "Subject",
            "HtmlBody",
            "TextBody",
            "Version",
            "IsActive",
            "CreatedAtUtc",
            "UpdatedAtUtc");

        await AssertIndexesAsync(
            "IX_InboxMessages_MessageId",
            "IX_Notifications_IdempotencyKey",
            "IX_Notifications_Status",
            "IX_Notifications_CorrelationId",
            "IX_Notifications_ScheduledAtUtc",
            "IX_NotificationDeliveryAttempts_NotificationId_AttemptNumber",
            "IX_NotificationTemplates_TemplateKey_Channel_Version");

        await AssertUniqueIndexesAsync(
            "IX_InboxMessages_MessageId",
            "IX_Notifications_IdempotencyKey",
            "IX_NotificationDeliveryAttempts_NotificationId_AttemptNumber",
            "IX_NotificationTemplates_TemplateKey_Channel_Version");

        await AssertTimestampWithTimeZoneColumnsAsync(
            ("InboxMessages", "ReceivedAtUtc"),
            ("InboxMessages", "ProcessedAtUtc"),
            ("Notifications", "RequestedAtUtc"),
            ("Notifications", "ScheduledAtUtc"),
            ("Notifications", "CreatedAtUtc"),
            ("Notifications", "SentAtUtc"),
            ("Notifications", "FailedAtUtc"),
            ("NotificationDeliveryAttempts", "StartedAtUtc"),
            ("NotificationDeliveryAttempts", "FinishedAtUtc"),
            ("NotificationTemplates", "CreatedAtUtc"),
            ("NotificationTemplates", "UpdatedAtUtc"));
    }

    [Fact]
    public async Task Migration_WhenApplied_ShouldSeedAuthEmailConfirmationTemplate()
    {
        if (!_fixture.IsAvailable)
            return;

        const string sql = """
            SELECT
                "TemplateKey",
                "Channel",
                "Subject",
                "HtmlBody",
                "TextBody",
                "Version",
                "IsActive"
            FROM "NotificationTemplates"
            WHERE "TemplateKey" = @TemplateKey
              AND "Channel" = 1
              AND "Version" = 1
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("TemplateKey", "auth.email-confirmation");

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal("auth.email-confirmation", reader.GetString(reader.GetOrdinal("TemplateKey")));
        Assert.Equal(1, reader.GetInt32(reader.GetOrdinal("Channel")));
        Assert.Equal("Confirme seu e-mail", reader.GetString(reader.GetOrdinal("Subject")));
        Assert.Contains("{{confirmationCode}}", reader.GetString(reader.GetOrdinal("TextBody")));
        Assert.Contains("{{expiresInMinutes}}", reader.GetString(reader.GetOrdinal("TextBody")));
        Assert.Contains("{{confirmationCode}}", reader.GetString(reader.GetOrdinal("HtmlBody")));
        Assert.Contains("{{expiresInMinutes}}", reader.GetString(reader.GetOrdinal("HtmlBody")));
        Assert.Equal(1, reader.GetInt32(reader.GetOrdinal("Version")));
        Assert.True(reader.GetBoolean(reader.GetOrdinal("IsActive")));
    }

    private async Task AssertColumnsAsync(string tableName, params string[] expectedColumns)
    {
        const string sql = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @TableName;
            """;

        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("TableName", tableName);

        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>();

        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.Contains(expectedColumn, columns);
        }
    }

    private async Task AssertIndexesAsync(params string[] expectedIndexes)
    {
        const string sql = """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'public';
            """;

        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var indexes = new HashSet<string>();

        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        foreach (var expectedIndex in expectedIndexes)
        {
            Assert.Contains(expectedIndex, indexes);
        }
    }

    private async Task AssertUniqueIndexesAsync(params string[] expectedIndexes)
    {
        const string sql = """
            SELECT indexrelid::regclass::text
            FROM pg_index
            WHERE indisunique = true;
            """;

        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var uniqueIndexes = new HashSet<string>();

        while (await reader.ReadAsync())
        {
            uniqueIndexes.Add(reader.GetString(0));
        }

        foreach (var expectedIndex in expectedIndexes)
        {
            Assert.Contains(expectedIndex, uniqueIndexes);
        }
    }

    private async Task AssertTimestampWithTimeZoneColumnsAsync(
        params (string TableName, string ColumnName)[] expectedColumns)
    {
        const string sql = """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @TableName
              AND column_name = @ColumnName;
            """;

        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();

        foreach (var expectedColumn in expectedColumns)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("TableName", expectedColumn.TableName);
            command.Parameters.AddWithValue("ColumnName", expectedColumn.ColumnName);

            var dataType = await command.ExecuteScalarAsync();

            Assert.Equal("timestamp with time zone", dataType);
        }
    }
}
