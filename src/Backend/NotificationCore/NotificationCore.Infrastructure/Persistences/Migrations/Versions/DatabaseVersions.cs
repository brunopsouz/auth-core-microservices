namespace NotificationCore.Infrastructure.Persistences.Migrations.Versions;

/// <summary>
/// Define as versões ordenadas das migrações do banco.
/// </summary>
public static class DatabaseVersions
{
    /// <summary>
    /// Versão de criação do schema inicial de notificações.
    /// </summary>
    public const long INITIAL_NOTIFICATION_SCHEMA = 1;

    /// <summary>
    /// Versão de seed dos templates transacionais iniciais.
    /// </summary>
    public const long INITIAL_NOTIFICATION_TEMPLATES = 2;
}
