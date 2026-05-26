using System.Text.Json;
using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Domain.Common.Repositories;
using AuthCore.IntegrationTests.Passports;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AuthCore.IntegrationTests.Services.Messaging;

/// <summary>
/// Verifica a persistência PostgreSQL das mensagens de outbox.
/// </summary>
public sealed class OutboxRepositoryPersistenceTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    /// <summary>
    /// Campo que armazena fixture.
    /// </summary>
    private readonly PostgreSqlIntegrationFixture _fixture;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="fixture">Fixture compartilhada de banco PostgreSQL.</param>
    public OutboxRepositoryPersistenceTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifica se o repositório persiste e atualiza uma mensagem de outbox.
    /// </summary>
    [Fact]
    public async Task Persistence_WhenOutboxMessageLifecycleChanges_ShouldPersistAndLoadState()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var occurredAtUtc = new DateTime(2026, 4, 28, 12, 0, 0, DateTimeKind.Utc);
        var outboxEvent = new EmailVerificationRequested
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Email = "user@example.com",
            Code = "123456",
            RequestedAtUtc = occurredAtUtc
        };
        var message = OutboxMessage.Create(
            nameof(EmailVerificationRequested),
            JsonSerializer.Serialize(outboxEvent),
            occurredAtUtc);

        await outboxRepository.AddAsync(message);

        var pendingMessages = await outboxRepository.GetPendingAsync(take: 10, maxAttempts: 3);
        var pendingMessage = Assert.Single(pendingMessages);

        Assert.Equal(message.Id, pendingMessage.Id);
        Assert.Equal(message.Type, pendingMessage.Type);
        Assert.Equal(message.Content, pendingMessage.Content);
        Assert.Equal(occurredAtUtc, pendingMessage.OccurredAtUtc);
        Assert.Null(pendingMessage.ProcessedAtUtc);
        Assert.Equal(0, pendingMessage.AttemptCount);
        Assert.Null(pendingMessage.LastError);

        var auditColumns = await GetAuditColumnsAsync(message.Id);

        Assert.Equal(occurredAtUtc, auditColumns.CreatedAt);
        Assert.Equal(occurredAtUtc, auditColumns.UpdateAt);
        Assert.True(auditColumns.IsActive);

        var processedAtUtc = occurredAtUtc.AddMinutes(1);
        await outboxRepository.UpdateAsync(message.MarkAsProcessed(processedAtUtc));

        var remainingMessages = await outboxRepository.GetPendingAsync(take: 10, maxAttempts: 3);
        var updatedAuditColumns = await GetAuditColumnsAsync(message.Id);

        Assert.Empty(remainingMessages);
        Assert.True(updatedAuditColumns.UpdateAt >= auditColumns.UpdateAt);
    }

    /// <summary>
    /// Operação para consultar as colunas técnicas da tabela de outbox.
    /// </summary>
    /// <param name="messageId">Identificador da mensagem.</param>
    /// <returns>Colunas técnicas persistidas.</returns>
    private async Task<(DateTime CreatedAt, DateTime UpdateAt, bool IsActive)> GetAuditColumnsAsync(Guid messageId)
    {
        const string sql = """
            SELECT
                "CreatedAt",
                "UpdateAt",
                "IsActive"
            FROM "OutboxMessages"
            WHERE "Id" = @Id;
            """;

        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("Id", messageId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return (
            reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            reader.GetDateTime(reader.GetOrdinal("UpdateAt")),
            reader.GetBoolean(reader.GetOrdinal("IsActive")));
    }
}
