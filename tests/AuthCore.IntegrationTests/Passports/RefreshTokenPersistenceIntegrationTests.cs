using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using AuthCore.Infrastructure;
using AuthCore.Infrastructure.Persistences.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AuthCore.IntegrationTests.Passports;

/// <summary>
/// Verifica a persistência PostgreSQL de refresh tokens e consultas auxiliares de usuário.
/// </summary>
public sealed class RefreshTokenPersistenceIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    /// <summary>
    /// Campo que armazena fixture.
    /// </summary>
    private readonly PostgreSqlIntegrationFixture _fixture;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="fixture">Fixture compartilhada de banco PostgreSQL.</param>
    public RefreshTokenPersistenceIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifica se o repositório persiste o ciclo de vida do refresh token e se a leitura por Id do usuário funciona.
    /// </summary>
    [Fact]
    public async Task Persistence_WhenRefreshTokenLifecycleChanges_ShouldPersistAndLoadState()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var userReadRepository = scope.ServiceProvider.GetRequiredService<IUserReadRepository>();
        var refreshTokenRepository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var nowUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc);
        var user = CreateVerifiedUser("refresh.lifecycle@example.com");

        await userRepository.AddAsync(user);

        var persistedUser = await userReadRepository.GetByIdAsync(user.Id);

        Assert.NotNull(persistedUser);
        Assert.Equal(user.Id, persistedUser!.Id);
        Assert.Equal(user.UserIdentifier, persistedUser.UserIdentifier);
        Assert.Equal(user.Email.Value, persistedUser.Email.Value);
        Assert.Equal(user.SecurityStamp, persistedUser.SecurityStamp);

        var initialToken = RefreshToken.IssueInitial(user.Id, " hash-inicial ", nowUtc.AddDays(7));
        await refreshTokenRepository.AddAsync(initialToken);

        var persistedInitialToken = await refreshTokenRepository.GetByHashAsync("hash-inicial");

        Assert.NotNull(persistedInitialToken);
        Assert.Equal(initialToken.Id, persistedInitialToken!.Id);
        Assert.Equal(initialToken.FamilyId, persistedInitialToken.FamilyId);
        Assert.Equal("hash-inicial", persistedInitialToken.TokenHash);
        Assert.Null(persistedInitialToken.ConsumedAtUtc);

        var replacementToken = RefreshToken.IssueReplacement(
            user.Id,
            initialToken.FamilyId,
            initialToken.Id,
            "hash-sucessor",
            nowUtc.AddDays(14));

        var consumedInitialToken = initialToken.Consume(replacementToken.Id, nowUtc.AddMinutes(5));

        await refreshTokenRepository.UpdateAsync(consumedInitialToken);
        await refreshTokenRepository.AddAsync(replacementToken);
        await refreshTokenRepository.RevokeFamilyAsync(initialToken.FamilyId, nowUtc.AddMinutes(10), "reuse-detected");

        var revokedInitialToken = await refreshTokenRepository.GetByHashAsync("hash-inicial");
        var revokedReplacementToken = await refreshTokenRepository.GetByHashAsync("hash-sucessor");

        Assert.NotNull(revokedInitialToken);
        Assert.NotNull(revokedReplacementToken);
        Assert.Equal(replacementToken.Id, revokedInitialToken!.ReplacedByTokenId);
        Assert.Equal(nowUtc.AddMinutes(5), revokedInitialToken.ConsumedAtUtc);
        Assert.Equal(nowUtc.AddMinutes(10), revokedInitialToken.RevokedAtUtc);
        Assert.Equal("reuse-detected", revokedInitialToken.RevocationReason);
        Assert.Equal(initialToken.FamilyId, revokedReplacementToken!.FamilyId);
        Assert.Equal(initialToken.Id, revokedReplacementToken.ParentTokenId);
        Assert.Equal(nowUtc.AddMinutes(10), revokedReplacementToken.RevokedAtUtc);
        Assert.Equal("reuse-detected", revokedReplacementToken.RevocationReason);
    }

    /// <summary>
    /// Verifica se a revogação por usuário afeta apenas refresh tokens ativos.
    /// </summary>
    [Fact]
    public async Task RevokeActiveByUserIdAsync_WhenUserHasMixedStates_ShouldRevokeOnlyActiveTokens()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var refreshTokenRepository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var nowUtc = new DateTime(2026, 4, 13, 15, 0, 0, DateTimeKind.Utc);
        var user = CreateVerifiedUser("refresh.revoke@example.com");
        var anotherUser = CreateVerifiedUser("refresh.other@example.com");

        await userRepository.AddAsync(user);
        await userRepository.AddAsync(anotherUser);

        var activeToken = RefreshToken.IssueInitial(user.Id, "hash-ativo", nowUtc.AddDays(7));
        var consumedToken = RefreshToken.IssueInitial(user.Id, "hash-consumido", nowUtc.AddDays(7))
            .Consume(Guid.NewGuid(), nowUtc.AddMinutes(-5));
        var expiredToken = RefreshToken.IssueInitial(user.Id, "hash-expirado", nowUtc.AddMinutes(-1));
        var otherUserToken = RefreshToken.IssueInitial(anotherUser.Id, "hash-outro-usuario", nowUtc.AddDays(7));

        await refreshTokenRepository.AddAsync(activeToken);
        await refreshTokenRepository.AddAsync(consumedToken);
        await refreshTokenRepository.AddAsync(expiredToken);
        await refreshTokenRepository.AddAsync(otherUserToken);

        await refreshTokenRepository.RevokeActiveByUserIdAsync(user.Id, nowUtc, "password-changed");

        var persistedActiveToken = await refreshTokenRepository.GetByHashAsync("hash-ativo");
        var persistedConsumedToken = await refreshTokenRepository.GetByHashAsync("hash-consumido");
        var persistedExpiredToken = await refreshTokenRepository.GetByHashAsync("hash-expirado");
        var persistedOtherUserToken = await refreshTokenRepository.GetByHashAsync("hash-outro-usuario");

        Assert.NotNull(persistedActiveToken);
        Assert.NotNull(persistedConsumedToken);
        Assert.NotNull(persistedExpiredToken);
        Assert.NotNull(persistedOtherUserToken);
        Assert.Equal(nowUtc, persistedActiveToken!.RevokedAtUtc);
        Assert.Equal("password-changed", persistedActiveToken.RevocationReason);
        Assert.Null(persistedConsumedToken!.RevokedAtUtc);
        Assert.Null(persistedExpiredToken!.RevokedAtUtc);
        Assert.Null(persistedOtherUserToken!.RevokedAtUtc);
    }

    /// <summary>
    /// Operação para criar um usuário verificado para os testes.
    /// </summary>
    /// <param name="email">E-mail do usuário.</param>
    /// <returns>Usuário pronto para persistência.</returns>
    private static User CreateVerifiedUser(string email)
    {
        var user = User.Register(
            firstName: "Auth",
            lastName: "Core",
            email: email,
            contact: "11999999999",
            role: Role.User);

        user.VerifyEmail(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));

        return user;
    }
}

/// <summary>
/// Representa a fixture compartilhada de integração com PostgreSQL.
/// </summary>
public sealed class PostgreSqlIntegrationFixture : IAsyncLifetime
{
    /// <summary>
    /// Campo que armazena database name.
    /// </summary>
    private readonly string _databaseName = $"auth_core_integration_{Guid.NewGuid():N}";
    /// <summary>
    /// Campo que armazena base connection string.
    /// </summary>
    private readonly string _baseConnectionString;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    public PostgreSqlIntegrationFixture()
    {
        _baseConnectionString = Environment.GetEnvironmentVariable("AUTHCORE_TEST_POSTGRES")
            ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;Pooling=false";
    }

    /// <summary>
    /// Provider de serviços configurado para o banco do teste.
    /// </summary>
    public ServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// String de conexão do banco criado para o teste atual.
    /// </summary>
    public string DatabaseConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Motivo do skip quando o banco não estiver disponível.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>
    /// Operação para inicializar o banco de integração.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!await CanConnectAsync())
        {
            SkipReason = "PostgreSQL de integração indisponível. Configure AUTHCORE_TEST_POSTGRES ou suba um servidor local acessível.";
            return;
        }

        DatabaseConnectionString = BuildDatabaseConnectionString(_databaseName);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = DatabaseConnectionString,
                ["Database:Migrations:AutoMigrateOnStartup"] = "true",
                ["Database:Migrations:EnsureDatabaseCreated"] = "true",
                ["Database:Migrations:AdminDatabase"] = GetAdminDatabaseName()
            })
            .Build();

        Services = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();

        await DatabaseMigration.MigrateAsync(Services);
    }

    /// <summary>
    /// Operação para liberar o banco criado para os testes.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (Services is not null)
            await Services.DisposeAsync();

        if (SkipReason is not null)
            return;

        await DropDatabaseAsync();
    }

    /// <summary>
    /// Indica se o banco de integração está disponível.
    /// </summary>
    public bool IsAvailable => SkipReason is null;

    /// <summary>
    /// Operação para verificar se a conexão administrativa está disponível.
    /// </summary>
    /// <returns><c>true</c> quando a conexão foi aberta; caso contrário, <c>false</c>.</returns>
    private async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_baseConnectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Operação para montar a string de conexão do banco do teste.
    /// </summary>
    /// <param name="databaseName">Nome do banco do teste.</param>
    /// <returns>String de conexão pronta para uso.</returns>
    private string BuildDatabaseConnectionString(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// Operação para obter o nome do banco administrativo.
    /// </summary>
    /// <returns>Nome do banco administrativo.</returns>
    private string GetAdminDatabaseName()
    {
        return new NpgsqlConnectionStringBuilder(_baseConnectionString).Database ?? "postgres";
    }

    /// <summary>
    /// Operação para remover o banco criado para o teste.
    /// </summary>
    private async Task DropDatabaseAsync()
    {
        var adminDatabase = GetAdminDatabaseName();
        var builder = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            Database = adminDatabase,
            Pooling = false
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        const string terminateSql = """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @DatabaseName
              AND pid <> pg_backend_pid();
            """;

        await using var terminateCommand = new NpgsqlCommand(terminateSql, connection);
        terminateCommand.Parameters.AddWithValue("DatabaseName", _databaseName);
        await terminateCommand.ExecuteNonQueryAsync();

        await using var dropCommand = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\";", connection);
        await dropCommand.ExecuteNonQueryAsync();
    }
}
