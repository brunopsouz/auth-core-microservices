namespace NotificationCore.Infrastructure.Persistences.Migrations.Versions;

/// <summary>
/// Define as versoes ordenadas das migracoes do banco.
/// </summary>
internal static class DatabaseVersions
{
    /// <summary>
    /// Versao de criacao do schema inicial de notificacoes.
    /// </summary>
    public const long INITIAL_NOTIFICATION_SCHEMA = 1;

    /// <summary>
    /// Versao de seed dos templates transacionais iniciais.
    /// </summary>
    public const long INITIAL_NOTIFICATION_TEMPLATES = 2;

    /// <summary>
    /// Versao de evolucao da inbox idempotente por consumidor.
    /// </summary>
    public const long IDEMPOTENT_CONSUMER_INBOX = 3;
}
