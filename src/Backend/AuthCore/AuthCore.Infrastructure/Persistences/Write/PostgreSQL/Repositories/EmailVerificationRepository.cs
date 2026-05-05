using AuthCore.Domain.Passports.Aggregates;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de verificação de e-mail.
/// </summary>
public sealed class EmailVerificationRepository : IEmailVerificationRepository
{
    private readonly IDatabaseSession _databaseSession;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public EmailVerificationRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }

    #endregion

    /// <summary>
    /// Operação para adicionar uma verificação de e-mail.
    /// </summary>
    /// <param name="emailVerification">Verificação a ser persistida.</param>
    public async Task AddAsync(EmailVerification emailVerification)
    {
        ArgumentNullException.ThrowIfNull(emailVerification);

        const string sql = """
            INSERT INTO "EmailVerificationTokens"
            (
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "UserId",
                "Email",
                "TokenHash",
                "ExpiresAtUtc",
                "ConsumedAtUtc",
                "RevokedAtUtc",
                "AttemptCount",
                "MaxAttempts",
                "CooldownUntilUtc",
                "LastSentAtUtc"
            )
            VALUES
            (
                @Id,
                @CreatedAt,
                @UpdateAt,
                @IsActive,
                @UserId,
                @Email,
                @TokenHash,
                @ExpiresAtUtc,
                @ConsumedAtUtc,
                @RevokedAtUtc,
                @AttemptCount,
                @MaxAttempts,
                @CooldownUntilUtc,
                @LastSentAtUtc
            );
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        AddParameters(command, emailVerification);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para atualizar uma verificação de e-mail.
    /// </summary>
    /// <param name="emailVerification">Verificação a ser atualizada.</param>
    public async Task UpdateAsync(EmailVerification emailVerification)
    {
        ArgumentNullException.ThrowIfNull(emailVerification);

        const string sql = """
            UPDATE "EmailVerificationTokens"
            SET
                "UpdateAt" = @UpdateAt,
                "IsActive" = @IsActive,
                "Email" = @Email,
                "TokenHash" = @TokenHash,
                "ExpiresAtUtc" = @ExpiresAtUtc,
                "ConsumedAtUtc" = @ConsumedAtUtc,
                "RevokedAtUtc" = @RevokedAtUtc,
                "AttemptCount" = @AttemptCount,
                "MaxAttempts" = @MaxAttempts,
                "CooldownUntilUtc" = @CooldownUntilUtc,
                "LastSentAtUtc" = @LastSentAtUtc
            WHERE "Id" = @Id;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        AddParameters(command, emailVerification);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para obter uma verificação pelo usuário, independente do estado atual.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Verificação encontrada ou nula.</returns>
    public async Task<EmailVerification?> GetByUserIdAsync(Guid userId)
    {
        var sql = """
            SELECT
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "UserId",
                "Email",
                "TokenHash",
                "ExpiresAtUtc",
                "ConsumedAtUtc",
                "RevokedAtUtc",
                "AttemptCount",
                "MaxAttempts",
                "CooldownUntilUtc",
                "LastSentAtUtc"
            FROM "EmailVerificationTokens"
            WHERE "UserId" = @UserId
            ORDER BY "LastSentAtUtc" DESC
            LIMIT 1
            """ + GetLockClause();

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadAsync(reader);
    }

    /// <summary>
    /// Operação para obter uma verificação pendente pelo e-mail.
    /// </summary>
    /// <param name="email">E-mail em verificação.</param>
    /// <returns>Verificação encontrada ou nula.</returns>
    public async Task<EmailVerification?> GetPendingByEmailAsync(string email)
    {
        var sql = """
            SELECT
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "UserId",
                "Email",
                "TokenHash",
                "ExpiresAtUtc",
                "ConsumedAtUtc",
                "RevokedAtUtc",
                "AttemptCount",
                "MaxAttempts",
                "CooldownUntilUtc",
                "LastSentAtUtc"
            FROM "EmailVerificationTokens"
            WHERE "Email" = @Email
              AND "ConsumedAtUtc" IS NULL
              AND "RevokedAtUtc" IS NULL
              AND "ExpiresAtUtc" > @ReferenceAtUtc
            ORDER BY "LastSentAtUtc" DESC
            LIMIT 1
            """ + GetLockClause();

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("Email", email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("ReferenceAtUtc", DateTime.UtcNow);

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadAsync(reader);
    }

    /// <summary>
    /// Operação para obter uma verificação ativa pelo usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Verificação encontrada ou nula.</returns>
    public async Task<EmailVerification?> GetPendingByUserIdAsync(Guid userId)
    {
        var sql = """
            SELECT
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "UserId",
                "Email",
                "TokenHash",
                "ExpiresAtUtc",
                "ConsumedAtUtc",
                "RevokedAtUtc",
                "AttemptCount",
                "MaxAttempts",
                "CooldownUntilUtc",
                "LastSentAtUtc"
            FROM "EmailVerificationTokens"
            WHERE "UserId" = @UserId
              AND "ConsumedAtUtc" IS NULL
              AND "RevokedAtUtc" IS NULL
              AND "ExpiresAtUtc" > @ReferenceAtUtc
            ORDER BY "LastSentAtUtc" DESC
            LIMIT 1
            """ + GetLockClause();

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("UserId", userId);
        command.Parameters.AddWithValue("ReferenceAtUtc", DateTime.UtcNow);

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadAsync(reader);
    }

    #region Helpers

    /// <summary>
    /// Operação para adicionar os parâmetros da verificação ao comando SQL.
    /// </summary>
    /// <param name="command">Comando SQL alvo.</param>
    /// <param name="emailVerification">Verificação persistida.</param>
    private static void AddParameters(NpgsqlCommand command, EmailVerification emailVerification)
    {
        command.Parameters.AddWithValue("Id", emailVerification.Id);
        command.Parameters.AddWithValue("CreatedAt", emailVerification.CreatedAt);
        command.Parameters.AddWithValue("UpdateAt", emailVerification.UpdateAt);
        command.Parameters.AddWithValue("IsActive", emailVerification.IsActive);
        command.Parameters.AddWithValue("UserId", emailVerification.UserId);
        command.Parameters.AddWithValue("Email", emailVerification.Email);
        command.Parameters.AddWithValue("TokenHash", emailVerification.CodeHash);
        command.Parameters.AddWithValue("ExpiresAtUtc", emailVerification.ExpiresAtUtc);
        command.Parameters.AddWithValue("ConsumedAtUtc", emailVerification.ConsumedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("RevokedAtUtc", emailVerification.RevokedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("AttemptCount", emailVerification.AttemptCount);
        command.Parameters.AddWithValue("MaxAttempts", emailVerification.MaxAttempts);
        command.Parameters.AddWithValue("CooldownUntilUtc", emailVerification.CooldownUntilUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("LastSentAtUtc", emailVerification.LastSentAtUtc);
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
    /// Operação para obter a cláusula de lock quando a leitura estiver dentro de transação.
    /// </summary>
    /// <returns>Cláusula SQL complementar.</returns>
    private string GetLockClause()
    {
        return _databaseSession.CurrentTransaction is null
            ? ";"
            : """
                FOR UPDATE;
                """;
    }

    /// <summary>
    /// Operação para materializar uma verificação a partir do leitor.
    /// </summary>
    /// <param name="reader">Leitor com os dados da verificação.</param>
    /// <returns>Verificação materializada ou nula.</returns>
    private static async Task<EmailVerification?> ReadAsync(NpgsqlDataReader reader)
    {
        if (!await reader.ReadAsync())
            return null;

        return EmailVerification.Restore(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            reader.GetDateTime(reader.GetOrdinal("UpdateAt")),
            reader.GetBoolean(reader.GetOrdinal("IsActive")),
            reader.GetGuid(reader.GetOrdinal("UserId")),
            reader.GetString(reader.GetOrdinal("Email")),
            reader.GetString(reader.GetOrdinal("TokenHash")),
            reader.GetDateTime(reader.GetOrdinal("ExpiresAtUtc")),
            reader.GetInt32(reader.GetOrdinal("AttemptCount")),
            reader.GetInt32(reader.GetOrdinal("MaxAttempts")),
            reader.IsDBNull(reader.GetOrdinal("CooldownUntilUtc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("CooldownUntilUtc")),
            reader.GetDateTime(reader.GetOrdinal("LastSentAtUtc")),
            reader.IsDBNull(reader.GetOrdinal("ConsumedAtUtc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("ConsumedAtUtc")),
            reader.IsDBNull(reader.GetOrdinal("RevokedAtUtc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("RevokedAtUtc")));
    }

    #endregion
}
