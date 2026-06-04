using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de sessão durável.
/// </summary>
internal sealed class DurableSessionRepository : IDurableSessionRepository
{
    /// <summary>
    /// Campo que armazena a sessão atual de banco de dados.
    /// </summary>
    private readonly IDatabaseSession _databaseSession;
    /// <summary>
    /// Campo que armazena o cálculo do hash do identificador opaco.
    /// </summary>
    private readonly ISessionIdentifierHasher _sessionIdentifierHasher;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    /// <param name="sessionIdentifierHasher">Serviço de hash do identificador opaco.</param>
    public DurableSessionRepository(
        IDatabaseSession databaseSession,
        ISessionIdentifierHasher sessionIdentifierHasher)
    {
        ArgumentNullException.ThrowIfNull(databaseSession);
        ArgumentNullException.ThrowIfNull(sessionIdentifierHasher);

        _databaseSession = databaseSession;
        _sessionIdentifierHasher = sessionIdentifierHasher;
    }


    /// <summary>
    /// Operação para adicionar uma sessão durável.
    /// </summary>
    /// <param name="session">Sessão a ser persistida.</param>
    public async Task AddAsync(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        const string sql = """
            INSERT INTO "auth_sessions"
            (
                "id",
                "public_session_id",
                "user_id",
                "session_identifier_hash",
                "status",
                "security_stamp",
                "device_name",
                "user_agent",
                "ip_address",
                "created_at_utc",
                "expires_at_utc",
                "last_seen_at_utc",
                "revoked_at_utc",
                "revocation_reason"
            )
            VALUES
            (
                @Id,
                @PublicSessionId,
                @UserId,
                @SessionIdentifierHash,
                @Status,
                @SecurityStamp,
                @DeviceName,
                @UserAgent,
                @IpAddress,
                @CreatedAtUtc,
                @ExpiresAtUtc,
                @LastSeenAtUtc,
                @RevokedAtUtc,
                @RevocationReason
            );
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("Id", Guid.NewGuid());
        command.Parameters.AddWithValue("PublicSessionId", session.PublicSessionId);
        command.Parameters.AddWithValue("UserId", session.UserId);
        command.Parameters.AddWithValue("SessionIdentifierHash", _sessionIdentifierHasher.ComputeHash(session.Identifier));
        command.Parameters.AddWithValue("Status", (short)session.Status);
        command.Parameters.AddWithValue("SecurityStamp", session.SecurityStamp.Value);
        command.Parameters.AddWithValue("DeviceName", DBNull.Value);
        command.Parameters.AddWithValue("UserAgent", session.UserAgent ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("IpAddress", session.IpAddress ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("CreatedAtUtc", session.CreatedAtUtc);
        command.Parameters.AddWithValue("ExpiresAtUtc", session.ExpiresAtUtc);
        command.Parameters.AddWithValue("LastSeenAtUtc", session.LastSeenAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("RevokedAtUtc", session.RevokedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("RevocationReason", session.RevocationReason.HasValue
            ? (short)session.RevocationReason.Value
            : (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para atualizar uma sessão durável.
    /// </summary>
    /// <param name="session">Sessão a ser atualizada.</param>
    public async Task UpdateAsync(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        const string sql = """
            UPDATE "auth_sessions"
            SET
                "status" = @Status,
                "security_stamp" = @SecurityStamp,
                "user_agent" = @UserAgent,
                "ip_address" = @IpAddress,
                "expires_at_utc" = @ExpiresAtUtc,
                "last_seen_at_utc" = @LastSeenAtUtc,
                "revoked_at_utc" = @RevokedAtUtc,
                "revocation_reason" = @RevocationReason
            WHERE "public_session_id" = @PublicSessionId;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("PublicSessionId", session.PublicSessionId);
        command.Parameters.AddWithValue("Status", (short)session.Status);
        command.Parameters.AddWithValue("SecurityStamp", session.SecurityStamp.Value);
        command.Parameters.AddWithValue("UserAgent", session.UserAgent ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("IpAddress", session.IpAddress ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("ExpiresAtUtc", session.ExpiresAtUtc);
        command.Parameters.AddWithValue("LastSeenAtUtc", session.LastSeenAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("RevokedAtUtc", session.RevokedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("RevocationReason", session.RevocationReason.HasValue
            ? (short)session.RevocationReason.Value
            : (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para obter sessão pelo hash do identificador opaco.
    /// </summary>
    /// <param name="sessionIdentifierHash">Hash do identificador opaco.</param>
    /// <param name="identifier">Identificador opaco original da sessão.</param>
    /// <returns>Sessão encontrada ou nula.</returns>
    public async Task<Session?> GetByIdentifierHashAsync(
        string sessionIdentifierHash,
        SessionIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        const string sql = """
            SELECT
                "public_session_id",
                "user_id",
                "status",
                "security_stamp",
                "user_agent",
                "ip_address",
                "created_at_utc",
                "expires_at_utc",
                "last_seen_at_utc",
                "revoked_at_utc",
                "revocation_reason"
            FROM "auth_sessions"
            WHERE "session_identifier_hash" = @SessionIdentifierHash
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("SessionIdentifierHash", NormalizeSessionIdentifierHash(sessionIdentifierHash));

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadSessionAsync(reader, identifier.Value);
    }

    /// <summary>
    /// Operação para obter sessão pelo identificador público.
    /// </summary>
    /// <param name="publicSessionId">Identificador público da sessão.</param>
    /// <returns>Sessão encontrada ou nula.</returns>
    public async Task<Session?> GetByPublicSessionIdAsync(string publicSessionId)
    {
        const string sql = """
            SELECT
                "public_session_id",
                "user_id",
                "status",
                "security_stamp",
                "user_agent",
                "ip_address",
                "created_at_utc",
                "expires_at_utc",
                "last_seen_at_utc",
                "revoked_at_utc",
                "revocation_reason"
            FROM "auth_sessions"
            WHERE "public_session_id" = @PublicSessionId
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("PublicSessionId", NormalizePublicSessionId(publicSessionId));

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadSessionAsync(reader, NormalizePublicSessionId(publicSessionId));
    }

    /// <summary>
    /// Operação para listar sessões de um usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Sessões encontradas.</returns>
    public async Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId)
    {
        const string sql = """
            SELECT
                "public_session_id",
                "user_id",
                "status",
                "security_stamp",
                "user_agent",
                "ip_address",
                "created_at_utc",
                "expires_at_utc",
                "last_seen_at_utc",
                "revoked_at_utc",
                "revocation_reason"
            FROM "auth_sessions"
            WHERE "user_id" = @UserId
            ORDER BY COALESCE("last_seen_at_utc", "created_at_utc") DESC, "created_at_utc" DESC;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadSessionsAsync(reader);
    }

    /// <summary>
    /// Operação para revogar sessões ativas de um usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="reason">Motivo da revogação.</param>
    /// <param name="revokedAtUtc">Data de revogação em UTC.</param>
    public async Task RevokeActiveByUserIdAsync(
        Guid userId,
        SessionRevocationReason reason,
        DateTime revokedAtUtc)
    {
        if (revokedAtUtc == default)
            throw new ArgumentException("A data de revogacao da sessao e obrigatoria.", nameof(revokedAtUtc));

        const string sql = """
            UPDATE "auth_sessions"
            SET
                "status" = @Status,
                "revoked_at_utc" = @RevokedAtUtc,
                "revocation_reason" = @RevocationReason
            WHERE "user_id" = @UserId
              AND "status" = @ActiveStatus
              AND "revoked_at_utc" IS NULL
              AND "expires_at_utc" > @ReferenceAtUtc;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("UserId", userId);
        command.Parameters.AddWithValue("Status", (short)SessionStatus.Revoked);
        command.Parameters.AddWithValue("ActiveStatus", (short)SessionStatus.Active);
        command.Parameters.AddWithValue("RevokedAtUtc", revokedAtUtc);
        command.Parameters.AddWithValue("RevocationReason", (short)reason);
        command.Parameters.AddWithValue("ReferenceAtUtc", revokedAtUtc);

        await command.ExecuteNonQueryAsync();
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
    /// Operação para materializar uma sessão a partir do leitor.
    /// </summary>
    /// <param name="reader">Leitor com os dados persistidos.</param>
    /// <param name="sessionId">Identificador opaco disponível para a materialização.</param>
    /// <returns>Sessão materializada ou nula.</returns>
    private static async Task<Session?> ReadSessionAsync(NpgsqlDataReader reader, string sessionId)
    {
        if (!await reader.ReadAsync())
            return null;

        return RestoreSession(reader, sessionId);
    }

    /// <summary>
    /// Operação para materializar uma coleção de sessões a partir do leitor.
    /// </summary>
    /// <param name="reader">Leitor com os dados persistidos.</param>
    /// <returns>Coleção materializada de sessões.</returns>
    private static async Task<IReadOnlyCollection<Session>> ReadSessionsAsync(NpgsqlDataReader reader)
    {
        var sessions = new List<Session>();

        while (await reader.ReadAsync())
        {
            var publicSessionId = reader.GetString(reader.GetOrdinal("public_session_id"));
            sessions.Add(RestoreSession(reader, publicSessionId));
        }

        return sessions;
    }

    /// <summary>
    /// Operação para reconstruir a sessão do domínio a partir do registro persistido.
    /// </summary>
    /// <param name="reader">Leitor com os dados da sessão.</param>
    /// <param name="sessionId">Identificador opaco disponível para a instância.</param>
    /// <returns>Sessão reconstruída.</returns>
    private static Session RestoreSession(NpgsqlDataReader reader, string sessionId)
    {
        return Session.Restore(
            sessionId,
            reader.GetString(reader.GetOrdinal("public_session_id")),
            reader.GetGuid(reader.GetOrdinal("user_id")),
            (SessionStatus)reader.GetInt16(reader.GetOrdinal("status")),
            reader.GetString(reader.GetOrdinal("security_stamp")),
            reader.GetDateTime(reader.GetOrdinal("created_at_utc")),
            reader.GetDateTime(reader.GetOrdinal("expires_at_utc")),
            reader.IsDBNull(reader.GetOrdinal("last_seen_at_utc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("last_seen_at_utc")),
            reader.IsDBNull(reader.GetOrdinal("ip_address"))
                ? null
                : reader.GetString(reader.GetOrdinal("ip_address")),
            reader.IsDBNull(reader.GetOrdinal("user_agent"))
                ? null
                : reader.GetString(reader.GetOrdinal("user_agent")),
            reader.IsDBNull(reader.GetOrdinal("revoked_at_utc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("revoked_at_utc")),
            reader.IsDBNull(reader.GetOrdinal("revocation_reason"))
                ? null
                : (SessionRevocationReason)reader.GetInt16(reader.GetOrdinal("revocation_reason")));
    }

    /// <summary>
    /// Operação para normalizar o hash informado na consulta.
    /// </summary>
    /// <param name="sessionIdentifierHash">Hash informado.</param>
    /// <returns>Hash normalizado.</returns>
    private static string NormalizeSessionIdentifierHash(string sessionIdentifierHash)
    {
        return string.IsNullOrWhiteSpace(sessionIdentifierHash)
            ? string.Empty
            : sessionIdentifierHash.Trim();
    }

    /// <summary>
    /// Operação para normalizar o identificador público informado.
    /// </summary>
    /// <param name="publicSessionId">Identificador público informado.</param>
    /// <returns>Identificador público normalizado.</returns>
    private static string NormalizePublicSessionId(string publicSessionId)
    {
        return string.IsNullOrWhiteSpace(publicSessionId)
            ? string.Empty
            : publicSessionId.Trim();
    }
}
