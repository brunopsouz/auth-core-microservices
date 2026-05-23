using AuthCore.Domain.Passports.Aggregates;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de refresh token.
/// </summary>
internal sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDatabaseSession _databaseSession;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public RefreshTokenRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }

    #endregion

    /// <summary>
    /// Operação para adicionar um refresh token.
    /// </summary>
    /// <param name="refreshToken">Refresh token a ser persistido.</param>
    public async Task AddAsync(RefreshToken refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);

        const string sql = """
            INSERT INTO "RefreshTokens"
            (
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "UserId",
                "FamilyId",
                "ParentTokenId",
                "ReplacedByTokenId",
                "TokenHash",
                "ExpiresAtUtc",
                "ConsumedAtUtc",
                "RevokedAtUtc",
                "RevocationReason"
            )
            VALUES
            (
                @Id,
                @CreatedAt,
                @UpdateAt,
                @IsActive,
                @UserId,
                @FamilyId,
                @ParentTokenId,
                @ReplacedByTokenId,
                @TokenHash,
                @ExpiresAtUtc,
                @ConsumedAtUtc,
                @RevokedAtUtc,
                @RevocationReason
            );
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("Id", refreshToken.Id);
        command.Parameters.AddWithValue("CreatedAt", refreshToken.CreatedAt);
        command.Parameters.AddWithValue("UpdateAt", refreshToken.UpdateAt);
        command.Parameters.AddWithValue("IsActive", refreshToken.IsActive);
        command.Parameters.AddWithValue("UserId", refreshToken.UserId);
        command.Parameters.AddWithValue("FamilyId", refreshToken.FamilyId);
        command.Parameters.AddWithValue("ParentTokenId", refreshToken.ParentTokenId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("ReplacedByTokenId", refreshToken.ReplacedByTokenId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("TokenHash", refreshToken.TokenHash);
        command.Parameters.AddWithValue("ExpiresAtUtc", refreshToken.ExpiresAtUtc);
        command.Parameters.AddWithValue("ConsumedAtUtc", refreshToken.ConsumedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("RevokedAtUtc", refreshToken.RevokedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("RevocationReason", refreshToken.RevocationReason ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para obter um refresh token pelo hash.
    /// </summary>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <returns>Refresh token encontrado ou nulo.</returns>
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
    {
        const string sql = """
            SELECT
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "UserId",
                "FamilyId",
                "ParentTokenId",
                "ReplacedByTokenId",
                "TokenHash",
                "ExpiresAtUtc",
                "ConsumedAtUtc",
                "RevokedAtUtc",
                "RevocationReason"
            FROM "RefreshTokens"
            WHERE "TokenHash" = @TokenHash
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("TokenHash", NormalizeTokenHash(tokenHash));

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadRefreshTokenAsync(reader);
    }

    /// <summary>
    /// Operação para atualizar um refresh token.
    /// </summary>
    /// <param name="refreshToken">Refresh token a ser atualizado.</param>
    public async Task UpdateAsync(RefreshToken refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);

        const string sql = """
            UPDATE "RefreshTokens"
            SET
                "UpdateAt" = @UpdateAt,
                "IsActive" = @IsActive,
                "ReplacedByTokenId" = @ReplacedByTokenId,
                "ConsumedAtUtc" = @ConsumedAtUtc,
                "RevokedAtUtc" = @RevokedAtUtc,
                "RevocationReason" = @RevocationReason
            WHERE "Id" = @Id;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("Id", refreshToken.Id);
        command.Parameters.AddWithValue("UpdateAt", refreshToken.UpdateAt);
        command.Parameters.AddWithValue("IsActive", refreshToken.IsActive);
        command.Parameters.AddWithValue("ReplacedByTokenId", refreshToken.ReplacedByTokenId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("ConsumedAtUtc", refreshToken.ConsumedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("RevokedAtUtc", refreshToken.RevokedAtUtc ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("RevocationReason", refreshToken.RevocationReason ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para revogar uma família de refresh tokens.
    /// </summary>
    /// <param name="familyId">Identificador da família de rotação.</param>
    /// <param name="revokedAtUtc">Data de revogação em UTC.</param>
    /// <param name="reason">Motivo operacional da revogação.</param>
    public async Task RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, string reason)
    {
        ValidateRevocationArguments(revokedAtUtc, reason);

        const string sql = """
            UPDATE "RefreshTokens"
            SET
                "UpdateAt" = @UpdateAt,
                "RevokedAtUtc" = @RevokedAtUtc,
                "RevocationReason" = @RevocationReason
            WHERE "FamilyId" = @FamilyId
              AND "RevokedAtUtc" IS NULL;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("FamilyId", familyId);
        command.Parameters.AddWithValue("UpdateAt", revokedAtUtc);
        command.Parameters.AddWithValue("RevokedAtUtc", revokedAtUtc);
        command.Parameters.AddWithValue("RevocationReason", NormalizeReason(reason));

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para revogar refresh tokens ativos de um usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="revokedAtUtc">Data de revogação em UTC.</param>
    /// <param name="reason">Motivo operacional da revogação.</param>
    public async Task RevokeActiveByUserIdAsync(Guid userId, DateTime revokedAtUtc, string reason)
    {
        ValidateRevocationArguments(revokedAtUtc, reason);

        const string sql = """
            UPDATE "RefreshTokens"
            SET
                "UpdateAt" = @UpdateAt,
                "RevokedAtUtc" = @RevokedAtUtc,
                "RevocationReason" = @RevocationReason
            WHERE "UserId" = @UserId
              AND "IsActive" = TRUE
              AND "ConsumedAtUtc" IS NULL
              AND "RevokedAtUtc" IS NULL
              AND "ExpiresAtUtc" > @ReferenceAtUtc;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("UserId", userId);
        command.Parameters.AddWithValue("UpdateAt", revokedAtUtc);
        command.Parameters.AddWithValue("RevokedAtUtc", revokedAtUtc);
        command.Parameters.AddWithValue("RevocationReason", NormalizeReason(reason));
        command.Parameters.AddWithValue("ReferenceAtUtc", revokedAtUtc);

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

    /// <summary>
    /// Operação para materializar um refresh token a partir do leitor.
    /// </summary>
    /// <param name="reader">Leitor com os dados do refresh token.</param>
    /// <returns>Refresh token materializado ou nulo.</returns>
    private static async Task<RefreshToken?> ReadRefreshTokenAsync(NpgsqlDataReader reader)
    {
        if (!await reader.ReadAsync())
            return null;

        return RefreshToken.Restore(
            id: reader.GetGuid(reader.GetOrdinal("Id")),
            createdAt: reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            updateAt: reader.GetDateTime(reader.GetOrdinal("UpdateAt")),
            isActive: reader.GetBoolean(reader.GetOrdinal("IsActive")),
            userId: reader.GetGuid(reader.GetOrdinal("UserId")),
            familyId: reader.GetGuid(reader.GetOrdinal("FamilyId")),
            parentTokenId: reader.IsDBNull(reader.GetOrdinal("ParentTokenId"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("ParentTokenId")),
            replacedByTokenId: reader.IsDBNull(reader.GetOrdinal("ReplacedByTokenId"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("ReplacedByTokenId")),
            tokenHash: reader.GetString(reader.GetOrdinal("TokenHash")),
            expiresAtUtc: reader.GetDateTime(reader.GetOrdinal("ExpiresAtUtc")),
            consumedAtUtc: reader.IsDBNull(reader.GetOrdinal("ConsumedAtUtc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("ConsumedAtUtc")),
            revokedAtUtc: reader.IsDBNull(reader.GetOrdinal("RevokedAtUtc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("RevokedAtUtc")),
            revocationReason: reader.IsDBNull(reader.GetOrdinal("RevocationReason"))
                ? null
                : reader.GetString(reader.GetOrdinal("RevocationReason")));
    }

    /// <summary>
    /// Operação para normalizar o hash persistido do refresh token.
    /// </summary>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <returns>Hash normalizado.</returns>
    private static string NormalizeTokenHash(string tokenHash)
    {
        return tokenHash.Trim();
    }

    /// <summary>
    /// Operação para normalizar o motivo operacional da revogação.
    /// </summary>
    /// <param name="reason">Motivo operacional da revogação.</param>
    /// <returns>Motivo normalizado.</returns>
    private static string NormalizeReason(string reason)
    {
        return reason.Trim();
    }

    /// <summary>
    /// Operação para validar os argumentos usados na revogação.
    /// </summary>
    /// <param name="revokedAtUtc">Data de revogação em UTC.</param>
    /// <param name="reason">Motivo operacional da revogação.</param>
    private static void ValidateRevocationArguments(DateTime revokedAtUtc, string reason)
    {
        if (revokedAtUtc == default)
            throw new ArgumentException("A data de revogação do refresh token é obrigatória.", nameof(revokedAtUtc));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("O motivo da revogação do refresh token é obrigatório.", nameof(reason));
    }

    #endregion
}
