using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.OUTBOX_PROCESSING_LEASE, "Add processing lease and optimized indexes to outbox")]
/// <summary>
/// Representa a migração de inclusão do lease de processamento da outbox.
/// </summary>
public sealed class Version0000012 : VersionBase
{
    /// <inheritdoc />
    public override void Up()
    {
        Alter.Table("OutboxMessages")
            .AddColumn("LeaseId").AsGuid().Nullable()
            .AddColumn("LeasedUntilUtc").AsCustom("timestamp with time zone").Nullable();

        Execute.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Pending_OccurredAtUtc"
            ON "OutboxMessages" ("OccurredAtUtc")
            WHERE "ProcessedAtUtc" IS NULL;

            CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_LeasedUntilUtc"
            ON "OutboxMessages" ("LeasedUntilUtc")
            WHERE "ProcessedAtUtc" IS NULL;
            """);
    }
}
