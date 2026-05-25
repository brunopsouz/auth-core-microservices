using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationCore.Infrastructure.Configurations;
using Npgsql;

namespace NotificationCore.Infrastructure.Persistences.Migrations;

/// <summary>
/// Define operações para aplicar migrações do banco de dados.
/// </summary>
internal static class DatabaseMigration
{
    /// <summary>
    /// Operação para criar o banco de dados e aplicar as migrações configuradas.
    /// </summary>
    /// <param name="serviceProvider">Provider de serviços da aplicação.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    public static async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        await using var scope = serviceProvider.CreateAsyncScope();

        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILoggerFactory>().CreateLogger("NotificationCore.Infrastructure.Persistences.Migrations");
        var databaseOptions = scopedServices.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var migrationOptions = scopedServices.GetRequiredService<IOptions<DatabaseMigrationOptions>>().Value;

        if (!migrationOptions.AutoMigrateOnStartup)
        {
            logger.LogInformation("Automatic database migrations are disabled.");
            return;
        }

        if (migrationOptions.EnsureDatabaseCreated)
        {
            await EnsureDatabaseCreatedAsync(
                databaseOptions.PostgreSql,
                migrationOptions.AdminDatabase,
                cancellationToken);
        }

        var runner = scopedServices.GetRequiredService<IMigrationRunner>();

        logger.LogInformation("Running FluentMigrator migrations.");

        runner.ListMigrations();
        runner.MigrateUp();

        logger.LogInformation("Database migrations applied successfully.");
    }

    /// <summary>
    /// Operação para garantir a criação do banco de dados configurado.
    /// </summary>
    /// <param name="connectionString">String de conexão do banco alvo.</param>
    /// <param name="adminDatabase">Banco administrativo usado na criação.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    private static async Task EnsureDatabaseCreatedAsync(
        string connectionString,
        string adminDatabase,
        CancellationToken cancellationToken)
    {
        var targetBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = targetBuilder.Database;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("The PostgreSQL database name must be present in the connection string.");
        }

        targetBuilder.Database = adminDatabase;

        await using var connection = new NpgsqlConnection(targetBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string existsSql = "SELECT 1 FROM pg_database WHERE datname = @databaseName;";
        await using var existsCommand = new NpgsqlCommand(existsSql, connection);
        existsCommand.Parameters.AddWithValue("databaseName", databaseName);

        var databaseExists = await existsCommand.ExecuteScalarAsync(cancellationToken) is not null;

        if (databaseExists)
        {
            return;
        }

        var quotedDatabaseName = QuoteIdentifier(databaseName);
        await using var createDatabaseCommand = new NpgsqlCommand($"CREATE DATABASE {quotedDatabaseName};", connection);
        await createDatabaseCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Operação para montar um identificador SQL escapado.
    /// </summary>
    /// <param name="identifier">Identificador a ser escapado.</param>
    /// <returns>Identificador pronto para uso em comando SQL.</returns>
    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
