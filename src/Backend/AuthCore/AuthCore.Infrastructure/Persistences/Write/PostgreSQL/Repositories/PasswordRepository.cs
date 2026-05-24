using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de senha.
/// </summary>
internal sealed class PasswordRepository : IPasswordRepository
{
    private readonly IDatabaseSession _databaseSession;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public PasswordRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }

    #endregion

    /// <summary>
    /// Operação para adicionar uma senha.
    /// </summary>
    /// <param name="password">Senha a ser persistida.</param>
    public async Task AddAsync(Password password)
    {
        ArgumentNullException.ThrowIfNull(password);

        const string sql = """
            INSERT INTO "Passwords"
            (
                "UserId",
                "Value",
                "Status",
                "FailedAttempts",
                "LastFailedAt",
                "LockedUntil"
            )
            VALUES
            (
                @UserId,
                @Value,
                @Status,
                @FailedAttempts,
                @LastFailedAt,
                @LockedUntil
            );
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("UserId", password.UserId);
        command.Parameters.AddWithValue("Value", password.Value);
        command.Parameters.AddWithValue("Status", (int)password.Status);
        command.Parameters.AddWithValue("FailedAttempts", password.LoginAttempt.FailedAttempts);
        command.Parameters.AddWithValue("LastFailedAt", password.LoginAttempt.LastFailedAt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("LockedUntil", password.LoginAttempt.LockedUntil ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para obter uma senha pelo identificador do usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Senha encontrada ou nula.</returns>
    public async Task<Password?> GetByUserIdAsync(Guid userId)
    {
        const string sql = """
            SELECT
                "UserId",
                "Value",
                "Status",
                "FailedAttempts",
                "LastFailedAt",
                "LockedUntil"
            FROM "Passwords"
            WHERE "UserId" = @UserId
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        var attempts = LoginAttempt.Restore(
            failedAttempts: reader.GetInt32(reader.GetOrdinal("FailedAttempts")),
            lastFailedAt: reader.IsDBNull(reader.GetOrdinal("LastFailedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LastFailedAt")),
            lockedUntil: reader.IsDBNull(reader.GetOrdinal("LockedUntil"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LockedUntil")));

        return Password.Restore(
            userId: reader.GetGuid(reader.GetOrdinal("UserId")),
            hashedPassword: reader.GetString(reader.GetOrdinal("Value")),
            attempts: attempts,
            status: (PasswordStatus)reader.GetInt32(reader.GetOrdinal("Status")));
    }

    /// <summary>
    /// Operação para atualizar uma senha.
    /// </summary>
    /// <param name="password">Senha a ser atualizada.</param>
    public async Task UpdateAsync(Password password)
    {
        ArgumentNullException.ThrowIfNull(password);

        const string sql = """
            UPDATE "Passwords"
            SET
                "Value" = @Value,
                "Status" = @Status,
                "FailedAttempts" = @FailedAttempts,
                "LastFailedAt" = @LastFailedAt,
                "LockedUntil" = @LockedUntil
            WHERE "UserId" = @UserId;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("UserId", password.UserId);
        command.Parameters.AddWithValue("Value", password.Value);
        command.Parameters.AddWithValue("Status", (int)password.Status);
        command.Parameters.AddWithValue("FailedAttempts", password.LoginAttempt.FailedAttempts);
        command.Parameters.AddWithValue("LastFailedAt", password.LoginAttempt.LastFailedAt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("LockedUntil", password.LoginAttempt.LockedUntil ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    #region Helpers

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

    #endregion
}
