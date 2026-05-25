using System.ComponentModel.DataAnnotations;

namespace NotificationCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de migração do banco de dados.
/// </summary>
internal sealed class DatabaseMigrationOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Database:Migrations";

    /// <summary>
    /// Indica se as migrações pendentes devem ser aplicadas na inicialização.
    /// </summary>
    public bool AutoMigrateOnStartup { get; init; } = false;

    /// <summary>
    /// Indica se o banco PostgreSQL deve ser criado automaticamente quando não existir.
    /// </summary>
    public bool EnsureDatabaseCreated { get; init; } = true;

    /// <summary>
    /// Banco administrativo usado antes da criação do banco alvo.
    /// </summary>
    [Required]
    public string AdminDatabase { get; init; } = "postgres";
}
