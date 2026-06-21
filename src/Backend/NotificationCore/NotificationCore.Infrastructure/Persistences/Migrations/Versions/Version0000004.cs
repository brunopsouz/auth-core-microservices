using FluentMigrator;

namespace NotificationCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.NOTIFICATION_QUERY_INDEXES, "Add optimized notification and inbox indexes")]
/// <summary>
/// Representa a migracao de otimizacao das consultas de notificacao.
/// </summary>
public sealed class Version0000004 : VersionBase
{
    /// <inheritdoc />
    public override void Up()
    {
        Execute.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_Notifications_Dispatch"
            ON "Notifications" ("Status", "ScheduledAtUtc", "Priority" DESC, "CreatedAtUtc");

            CREATE INDEX IF NOT EXISTS "IX_InboxMessages_IdempotencyKey"
            ON "InboxMessages" (("Payload"::jsonb ->> 'IdempotencyKey'));
            """);
    }
}
