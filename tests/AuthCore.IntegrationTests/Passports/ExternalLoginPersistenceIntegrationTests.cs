using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure.Abstractions.Data;
using AuthCore.Infrastructure.Persistences.Write.PostgreSQL.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AuthCore.IntegrationTests.Passports;

/// <summary>
/// Verifica a persistência PostgreSQL de logins externos.
/// </summary>
public sealed class ExternalLoginPersistenceIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    /// <summary>
    /// Campo que armazena fixture.
    /// </summary>
    private readonly PostgreSqlIntegrationFixture _fixture;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="fixture">Fixture compartilhada de banco PostgreSQL.</param>
    public ExternalLoginPersistenceIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifica se o repositório persiste todos os campos do login externo.
    /// </summary>
    [Fact]
    public async Task AddAsync_WhenExternalLoginIsValid_ShouldPersist()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var databaseSession = scope.ServiceProvider.GetRequiredService<IDatabaseSession>();
        var externalLoginRepository = new ExternalLoginRepository(databaseSession);
        var user = CreateVerifiedUser("external.add@example.com");
        var linkedAtUtc = new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc);
        var externalLogin = ExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-add",
            "external.add@example.com",
            emailVerified: true,
            linkedAtUtc);

        await userRepository.AddAsync(user);
        await externalLoginRepository.AddAsync(externalLogin);

        var persistedExternalLogin = await GetExternalLoginRowAsync(externalLogin.Id);

        Assert.NotNull(persistedExternalLogin);
        Assert.Equal(externalLogin.Id, persistedExternalLogin!.Id);
        Assert.InRange(persistedExternalLogin.CreatedAt, externalLogin.CreatedAt.AddMilliseconds(-1), externalLogin.CreatedAt.AddMilliseconds(1));
        Assert.InRange(persistedExternalLogin.UpdateAt, externalLogin.UpdateAt.AddMilliseconds(-1), externalLogin.UpdateAt.AddMilliseconds(1));
        Assert.Equal(externalLogin.IsActive, persistedExternalLogin.IsActive);
        Assert.Equal(user.Id, persistedExternalLogin.UserId);
        Assert.Equal((short)ExternalLoginProvider.Google, persistedExternalLogin.Provider);
        Assert.Equal("google-sub-add", persistedExternalLogin.ProviderUserId);
        Assert.Equal("external.add@example.com", persistedExternalLogin.Email);
        Assert.True(persistedExternalLogin.EmailVerified);
        Assert.Equal(linkedAtUtc, persistedExternalLogin.LinkedAtUtc);
        Assert.Equal(linkedAtUtc, persistedExternalLogin.LastUsedAtUtc);
    }

    /// <summary>
    /// Verifica se o repositório persiste a data de último uso sem alterar o identificador externo.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenUsageIsRegistered_ShouldPersistLastUsedAtUtc()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var databaseSession = scope.ServiceProvider.GetRequiredService<IDatabaseSession>();
        var externalLoginRepository = new ExternalLoginRepository(databaseSession);
        var user = CreateVerifiedUser("external.update@example.com");
        var linkedAtUtc = new DateTime(2026, 6, 14, 11, 0, 0, DateTimeKind.Utc);
        var usedAtUtc = linkedAtUtc.AddMinutes(15);
        var externalLogin = ExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-update",
            "external.update@example.com",
            emailVerified: true,
            linkedAtUtc);

        await userRepository.AddAsync(user);
        await externalLoginRepository.AddAsync(externalLogin);

        externalLogin.RegisterUsage(usedAtUtc);
        await externalLoginRepository.UpdateAsync(externalLogin);

        var persistedExternalLogin = await GetExternalLoginRowAsync(externalLogin.Id);

        Assert.NotNull(persistedExternalLogin);
        Assert.Equal((short)ExternalLoginProvider.Google, persistedExternalLogin!.Provider);
        Assert.Equal("google-sub-update", persistedExternalLogin.ProviderUserId);
        Assert.Equal(usedAtUtc, persistedExternalLogin.LastUsedAtUtc);
        Assert.True(persistedExternalLogin.UpdateAt >= persistedExternalLogin.CreatedAt);
    }

    /// <summary>
    /// Operação para consultar login externo persistido.
    /// </summary>
    /// <param name="externalLoginId">Identificador do login externo.</param>
    /// <returns>Dados persistidos do login externo ou nulo.</returns>
    private async Task<ExternalLoginRow?> GetExternalLoginRowAsync(Guid externalLoginId)
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
            WHERE id = @Id;
            """;

        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("Id", externalLoginId);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new ExternalLoginRow(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetDateTime(reader.GetOrdinal("created_at")),
            reader.GetDateTime(reader.GetOrdinal("update_at")),
            reader.GetBoolean(reader.GetOrdinal("is_active")),
            reader.GetGuid(reader.GetOrdinal("user_id")),
            reader.GetInt16(reader.GetOrdinal("provider")),
            reader.GetString(reader.GetOrdinal("provider_user_id")),
            reader.GetString(reader.GetOrdinal("email")),
            reader.GetBoolean(reader.GetOrdinal("email_verified")),
            reader.GetDateTime(reader.GetOrdinal("linked_at_utc")),
            reader.IsDBNull(reader.GetOrdinal("last_used_at_utc"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("last_used_at_utc")));
    }

    /// <summary>
    /// Operação para criar um usuário verificado para os testes.
    /// </summary>
    /// <param name="email">E-mail do usuário.</param>
    /// <returns>Usuário pronto para persistência.</returns>
    private static User CreateVerifiedUser(string email)
    {
        var user = User.Register(
            firstName: "External",
            lastName: "Login",
            email: email,
            contact: "11999999999",
            role: Role.User);

        user.VerifyEmail(new DateTime(2026, 6, 14, 9, 0, 0, DateTimeKind.Utc));

        return user;
    }

    /// <summary>
    /// Representa dados persistidos de login externo.
    /// </summary>
    private sealed class ExternalLoginRow
    {
        /// <summary>
        /// Operação para criar instância da classe.
        /// </summary>
        public ExternalLoginRow(
            Guid id,
            DateTime createdAt,
            DateTime updateAt,
            bool isActive,
            Guid userId,
            short provider,
            string providerUserId,
            string email,
            bool emailVerified,
            DateTime linkedAtUtc,
            DateTime? lastUsedAtUtc)
        {
            Id = id;
            CreatedAt = createdAt;
            UpdateAt = updateAt;
            IsActive = isActive;
            UserId = userId;
            Provider = provider;
            ProviderUserId = providerUserId;
            Email = email;
            EmailVerified = emailVerified;
            LinkedAtUtc = linkedAtUtc;
            LastUsedAtUtc = lastUsedAtUtc;
        }

        /// <summary>
        /// Identificador do login externo.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Data de criação.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Data de atualização.
        /// </summary>
        public DateTime UpdateAt { get; }

        /// <summary>
        /// Indica se o registro está ativo.
        /// </summary>
        public bool IsActive { get; }

        /// <summary>
        /// Identificador interno do usuário.
        /// </summary>
        public Guid UserId { get; }

        /// <summary>
        /// Provedor externo.
        /// </summary>
        public short Provider { get; }

        /// <summary>
        /// Identificador do usuário no provedor externo.
        /// </summary>
        public string ProviderUserId { get; }

        /// <summary>
        /// E-mail informado pelo provedor.
        /// </summary>
        public string Email { get; }

        /// <summary>
        /// Indica se o e-mail foi verificado pelo provedor.
        /// </summary>
        public bool EmailVerified { get; }

        /// <summary>
        /// Data de vínculo em UTC.
        /// </summary>
        public DateTime LinkedAtUtc { get; }

        /// <summary>
        /// Data do último uso em UTC.
        /// </summary>
        public DateTime? LastUsedAtUtc { get; }
    }
}
