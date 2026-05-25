namespace AuthCore.Infrastructure.Persistences.Migrations;

/// <summary>
/// Define operações para aplicar migrações da infraestrutura.
/// </summary>
internal static class ServiceProviderExtensions
{
    /// <summary>
    /// Operação para aplicar as migrações de infraestrutura.
    /// </summary>
    /// <param name="serviceProvider">Provider de serviços da aplicação.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Task da operação assíncrona.</returns>
    public static Task ApplyInfrastructureMigrationsAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return DatabaseMigration.MigrateAsync(serviceProvider, cancellationToken);
    }
}
