using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using Npgsql;

namespace AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de login externo.
/// </summary>
internal sealed class ExternalLoginRepository : IExternalLoginRepository
{
    /// <summary>
    /// Campo que armazena database session.
    /// </summary>
    private readonly IDatabaseSession _databaseSession;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public ExternalLoginRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }


    /// <summary>
    /// Operação para adicionar um login externo.
    /// </summary>
    /// <param name="externalLogin">Login externo a ser persistido.</param>
    public async Task AddAsync(ExternalLogin externalLogin)
    {
        await AddAsync(externalLogin, CancellationToken.None);
    }

    public async Task AddAsync(
        ExternalLogin externalLogin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(externalLogin);

        const string sql = """
            INSERT INTO external_logins
            (
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
            )
            VALUES
            (
                @Id,
                @CreatedAt,
                @UpdateAt,
                @IsActive,
                @UserId,
                @Provider,
                @ProviderUserId,
                @Email,
                @EmailVerified,
                @LinkedAtUtc,
                @LastUsedAtUtc
            );
            """;

        await using var connectionLease = await _databaseSession.AcquireConnectionAsync(cancellationToken);
        var connection = connectionLease.Connection;
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("Id", externalLogin.Id);
        command.Parameters.AddWithValue("CreatedAt", externalLogin.CreatedAt);
        command.Parameters.AddWithValue("UpdateAt", externalLogin.UpdateAt);
        command.Parameters.AddWithValue("IsActive", externalLogin.IsActive);
        command.Parameters.AddWithValue("UserId", externalLogin.UserId);
        command.Parameters.AddWithValue("Provider", (short)externalLogin.Provider);
        command.Parameters.AddWithValue("ProviderUserId", externalLogin.ProviderUserId);
        command.Parameters.AddWithValue("Email", externalLogin.Email);
        command.Parameters.AddWithValue("EmailVerified", externalLogin.EmailVerified);
        command.Parameters.AddWithValue("LinkedAtUtc", externalLogin.LinkedAtUtc);
        command.Parameters.AddWithValue("LastUsedAtUtc", externalLogin.LastUsedAtUtc ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Operação para atualizar um login externo.
    /// </summary>
    /// <param name="externalLogin">Login externo a ser atualizado.</param>
    public async Task UpdateAsync(ExternalLogin externalLogin)
    {
        await UpdateAsync(externalLogin, CancellationToken.None);
    }

    public async Task UpdateAsync(
        ExternalLogin externalLogin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(externalLogin);

        const string sql = """
            UPDATE external_logins
            SET
                update_at = @UpdateAt,
                is_active = @IsActive,
                email = @Email,
                email_verified = @EmailVerified,
                last_used_at_utc = @LastUsedAtUtc
            WHERE id = @Id;
            """;

        await using var connectionLease = await _databaseSession.AcquireConnectionAsync(cancellationToken);
        var connection = connectionLease.Connection;
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("Id", externalLogin.Id);
        command.Parameters.AddWithValue("UpdateAt", externalLogin.UpdateAt);
        command.Parameters.AddWithValue("IsActive", externalLogin.IsActive);
        command.Parameters.AddWithValue("Email", externalLogin.Email);
        command.Parameters.AddWithValue("EmailVerified", externalLogin.EmailVerified);
        command.Parameters.AddWithValue("LastUsedAtUtc", externalLogin.LastUsedAtUtc ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Operacao para excluir um login externo.
    /// </summary>
    /// <param name="externalLogin">Login externo a ser excluido.</param>
    public async Task DeleteAsync(ExternalLogin externalLogin)
    {
        ArgumentNullException.ThrowIfNull(externalLogin);

        const string sql = """
            DELETE FROM external_logins
            WHERE id = @Id;
            """;

        await using var connectionLease = await _databaseSession.AcquireConnectionAsync();
        var connection = connectionLease.Connection;
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("Id", externalLogin.Id);

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
