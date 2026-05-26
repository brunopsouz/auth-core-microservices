using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de usuário.
/// </summary>
internal sealed class UserRepository : IUserRepository
{
    /// <summary>
    /// Campo que armazena database session.
    /// </summary>
    private readonly IDatabaseSession _databaseSession;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public UserRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }


    /// <summary>
    /// Operação para adicionar um usuário.
    /// </summary>
    /// <param name="user">Usuário a ser persistido.</param>
    public async Task AddAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        const string sql = """
            INSERT INTO "Users"
            (
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
            )
            VALUES
            (
                @Id,
                @CreatedAt,
                @UpdateAt,
                @IsActive,
                @FirstName,
                @LastName,
                @FullName,
                @Email,
                @Contact,
                @UserIdentifier,
                @Role,
                @Status,
                @EmailVerifiedAt
            );
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("Id", user.Id);
        command.Parameters.AddWithValue("CreatedAt", user.CreatedAt);
        command.Parameters.AddWithValue("UpdateAt", user.UpdateAt);
        command.Parameters.AddWithValue("IsActive", user.IsActive);
        command.Parameters.AddWithValue("FirstName", user.FirstName);
        command.Parameters.AddWithValue("LastName", user.LastName);
        command.Parameters.AddWithValue("FullName", user.FullName);
        command.Parameters.AddWithValue("Email", user.Email.Value);
        command.Parameters.AddWithValue("Contact", user.Contact);
        command.Parameters.AddWithValue("UserIdentifier", user.UserIdentifier);
        command.Parameters.AddWithValue("Role", (int)user.Role);
        command.Parameters.AddWithValue("Status", (int)user.Status);
        command.Parameters.AddWithValue("EmailVerifiedAt", user.EmailVerifiedAt ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para atualizar um usuário.
    /// </summary>
    /// <param name="user">Usuário a ser atualizado.</param>
    public async Task UpdateAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        const string sql = """
            UPDATE "Users"
            SET
                "UpdateAt" = @UpdateAt,
                "IsActive" = @IsActive,
                "FirstName" = @FirstName,
                "LastName" = @LastName,
                "FullName" = @FullName,
                "Email" = @Email,
                "Contact" = @Contact,
                "Role" = @Role,
                "Status" = @Status,
                "EmailVerifiedAt" = @EmailVerifiedAt
            WHERE "Id" = @Id;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("Id", user.Id);
        command.Parameters.AddWithValue("UpdateAt", user.UpdateAt);
        command.Parameters.AddWithValue("IsActive", user.IsActive);
        command.Parameters.AddWithValue("FirstName", user.FirstName);
        command.Parameters.AddWithValue("LastName", user.LastName);
        command.Parameters.AddWithValue("FullName", user.FullName);
        command.Parameters.AddWithValue("Email", user.Email.Value);
        command.Parameters.AddWithValue("Contact", user.Contact);
        command.Parameters.AddWithValue("Role", (int)user.Role);
        command.Parameters.AddWithValue("Status", (int)user.Status);
        command.Parameters.AddWithValue("EmailVerifiedAt", user.EmailVerifiedAt ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Operação para remover um usuário.
    /// </summary>
    /// <param name="user">Usuário a ser removido.</param>
    public async Task DeleteAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        const string sql = """
            DELETE FROM "Users"
            WHERE "Id" = @Id;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        command.Parameters.AddWithValue("Id", user.Id);

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

}
