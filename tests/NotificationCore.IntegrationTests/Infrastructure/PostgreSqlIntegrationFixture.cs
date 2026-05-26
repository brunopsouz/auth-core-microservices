using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationCore.Infrastructure;
using NotificationCore.Infrastructure.Persistences.Migrations;
using Npgsql;

namespace NotificationCore.IntegrationTests.Infrastructure;

/// <summary>
/// Representa a fixture compartilhada de integração com PostgreSQL.
/// </summary>
public sealed class PostgreSqlIntegrationFixture : IAsyncLifetime
{
    /// <summary>
    /// Campo que armazena database name.
    /// </summary>
    private readonly string _databaseName = $"notification_core_integration_{Guid.NewGuid():N}";
    /// <summary>
    /// Campo que armazena base connection string.
    /// </summary>
    private readonly string _baseConnectionString;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    public PostgreSqlIntegrationFixture()
    {
        _baseConnectionString = Environment.GetEnvironmentVariable("NOTIFICATIONCORE_TEST_POSTGRES")
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
            SkipReason = "PostgreSQL de integração indisponível. Configure NOTIFICATIONCORE_TEST_POSTGRES ou suba um servidor local acessível.";
            return;
        }

        DatabaseConnectionString = BuildDatabaseConnectionString(_databaseName);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = DatabaseConnectionString,
                ["Database:Migrations:AutoMigrateOnStartup"] = "true",
                ["Database:Migrations:EnsureDatabaseCreated"] = "true",
                ["Database:Migrations:AdminDatabase"] = GetAdminDatabaseName(),
                ["RabbitMq:Host"] = "localhost",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["Smtp:Host"] = "localhost",
                ["Smtp:SenderEmail"] = "notifications@example.com"
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
