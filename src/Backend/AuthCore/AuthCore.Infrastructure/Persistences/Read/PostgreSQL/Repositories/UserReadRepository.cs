using AuthCore.Domain.Users.Aggregates;
using AuthCore.Domain.Users.Enums;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Read.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de leitura de usuário.
/// </summary>
internal sealed class UserReadRepository : IUserReadRepository
{
    private readonly IDatabaseSession _databaseSession;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public UserReadRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }

    #endregion

    /// <summary>
    /// Operação para obter um usuário pelo identificador interno.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Usuário encontrado ou nulo.</returns>
    public async Task<User?> GetByIdAsync(Guid userId)
    {
        const string sql = """
            SELECT
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "FirstName",
                "LastName",
                "FullName",
                "Email",
                "Contact",
                "UserIdentifier",
                "Role",
                "Status",
                "EmailVerifiedAt"
            FROM "Users"
            WHERE "Id" = @UserId
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadUserAsync(reader);
    }

    /// <summary>
    /// Operação para obter um usuário pelo identificador público.
    /// </summary>
    /// <param name="userIdentifier">Identificador público do usuário.</param>
    /// <returns>Usuário encontrado ou nulo.</returns>
    public async Task<User?> GetByUserIdentifierAsync(Guid userIdentifier)
    {
        const string sql = """
            SELECT
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "FirstName",
                "LastName",
                "FullName",
                "Email",
                "Contact",
                "UserIdentifier",
                "Role",
                "Status",
                "EmailVerifiedAt"
            FROM "Users"
            WHERE "UserIdentifier" = @UserIdentifier
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("UserIdentifier", userIdentifier);

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadUserAsync(reader);
    }

    /// <summary>
    /// Operação para obter um usuário pelo e-mail.
    /// </summary>
    /// <param name="email">E-mail do usuário.</param>
    /// <returns>Usuário encontrado ou nulo.</returns>
    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = """
            SELECT
                "Id",
                "CreatedAt",
                "UpdateAt",
                "IsActive",
                "FirstName",
                "LastName",
                "FullName",
                "Email",
                "Contact",
                "UserIdentifier",
                "Role",
                "Status",
                "EmailVerifiedAt"
            FROM "Users"
            WHERE "Email" = @Email
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("Email", email.Trim().ToLowerInvariant());

        await using var reader = await command.ExecuteReaderAsync();

        return await ReadUserAsync(reader);
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
    /// Operação para materializar um usuário a partir do leitor.
    /// </summary>
    /// <param name="reader">Leitor com os dados do usuário.</param>
    /// <returns>Usuário materializado ou nulo.</returns>
    private static async Task<User?> ReadUserAsync(NpgsqlDataReader reader)
    {
        if (!await reader.ReadAsync())
            return null;

        return User.Restore(
            id: reader.GetGuid(reader.GetOrdinal("Id")),
            createdAt: reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            updateAt: reader.GetDateTime(reader.GetOrdinal("UpdateAt")),
            isActive: reader.GetBoolean(reader.GetOrdinal("IsActive")),
            firstName: reader.GetString(reader.GetOrdinal("FirstName")),
            lastName: reader.GetString(reader.GetOrdinal("LastName")),
            fullName: reader.GetString(reader.GetOrdinal("FullName")),
            email: reader.GetString(reader.GetOrdinal("Email")),
            contact: reader.GetString(reader.GetOrdinal("Contact")),
            role: (Role)reader.GetInt32(reader.GetOrdinal("Role")),
            status: (UserStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            userIdentifier: reader.GetGuid(reader.GetOrdinal("UserIdentifier")),
            emailVerifiedAt: reader.IsDBNull(reader.GetOrdinal("EmailVerifiedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("EmailVerifiedAt")));
    }

    #endregion
}
