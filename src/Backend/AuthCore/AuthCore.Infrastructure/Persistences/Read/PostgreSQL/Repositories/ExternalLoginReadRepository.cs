using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Read.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de leitura de login externo.
/// </summary>
internal sealed class ExternalLoginReadRepository : IExternalLoginReadRepository
{
    /// <summary>
    /// Campo que armazena database session.
    /// </summary>
    private readonly IDatabaseSession _databaseSession;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public ExternalLoginReadRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }


    /// <summary>
    /// Operação para obter um login externo pelo provedor e identificador externo.
    /// </summary>
    /// <param name="provider">Provedor externo de login.</param>
    /// <param name="providerUserId">Identificador do usuário no provedor externo.</param>
    /// <returns>Login externo encontrado ou nulo.</returns>
    public async Task<ExternalLogin?> GetByProviderUserIdAsync(
        ExternalLoginProvider provider,
        string providerUserId)
    {
        const string sql = """
            SELECT
                id,
                created_at,
                update_at,
                is_active,
                user_id,
                provider,
                provider_user_id,
                email,
                email_verified,
                linked_at_utc,
                last_used_at_utc
            FROM external_logins
            WHERE provider = @Provider
              AND provider_user_id = @ProviderUserId
            LIMIT 1;
            """;

        await using var connectionLease = await _databaseSession.AcquireConnectionAsync();

        var connection = connectionLease.Connection;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("Provider", (short)provider);
        command.Parameters.AddWithValue("ProviderUserId", providerUserId.Trim());

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadExternalLoginAsync(reader);
    }

    /// <summary>
    /// Operação para obter um login externo pelo usuário e provedor.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="provider">Provedor externo de login.</param>
    /// <returns>Login externo encontrado ou nulo.</returns>
    public async Task<ExternalLogin?> GetByUserIdAndProviderAsync(
        Guid userId,
        ExternalLoginProvider provider)
    {
        const string sql = """
            SELECT
                id,
                created_at,
                update_at,
                is_active,
                user_id,
                provider,
                provider_user_id,
                email,
                email_verified,
                linked_at_utc,
                last_used_at_utc
            FROM external_logins
            WHERE user_id = @UserId
              AND provider = @Provider
            LIMIT 1;
            """;

        await using var connectionLease = await _databaseSession.AcquireConnectionAsync();

        var connection = connectionLease.Connection;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("UserId", userId);
        command.Parameters.AddWithValue("Provider", (short)provider);

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadExternalLoginAsync(reader);
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
    /// Operação para materializar um login externo a partir do leitor.
    /// </summary>
    /// <param name="reader">Leitor com os dados do login externo.</param>
    /// <returns>Login externo materializado ou nulo.</returns>
    private static async Task<ExternalLogin?> ReadExternalLoginAsync(NpgsqlDataReader reader)
    {
        if (!await reader.ReadAsync())
            return null;

        return ExternalLogin.Restore(
            id: reader.GetGuid(reader.GetOrdinal("id")),
            createdAt: reader.GetDateTime(reader.GetOrdinal("created_at")),
            updateAt: reader.GetDateTime(reader.GetOrdinal("update_at")),
            isActive: reader.GetBoolean(reader.GetOrdinal("is_active")),
            userId: reader.GetGuid(reader.GetOrdinal("user_id")),
            provider: (ExternalLoginProvider)reader.GetInt16(reader.GetOrdinal("provider")),
            providerUserId: reader.GetString(reader.GetOrdinal("provider_user_id")),
            email: reader.GetString(reader.GetOrdinal("email")),
            emailVerified: reader.GetBoolean(reader.GetOrdinal("email_verified")),
            linkedAtUtc: reader.GetDateTime(reader.GetOrdinal("linked_at_utc")),
            lastUsedAtUtc: reader.IsDBNull(reader.GetOrdinal("last_used_at_utc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("last_used_at_utc")));
    }
}
