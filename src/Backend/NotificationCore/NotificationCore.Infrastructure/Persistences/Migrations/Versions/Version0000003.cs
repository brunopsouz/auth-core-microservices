using FluentMigrator;

namespace NotificationCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.IDEMPOTENT_CONSUMER_INBOX, "Evolve inbox messages for idempotent consumers")]
/// <summary>
/// Representa a migracao de evolucao da inbox por consumidor.
/// </summary>
public sealed class Version0000003 : VersionBase
{
    /// <summary>
    /// Operacao para aplicar a migracao da versao atual.
    /// </summary>
    public override void Up()
    {
        Execute.Sql("""
            ALTER TABLE "InboxMessages"
            ADD COLUMN IF NOT EXISTS "Id" uuid;

            UPDATE "InboxMessages"
            SET "Id" = uuid_in(md5(random()::text || clock_timestamp()::text)::cstring)
            WHERE "Id" IS NULL;

            ALTER TABLE "InboxMessages"
            ALTER COLUMN "Id" SET NOT NULL;

            DO $$
            BEGIN
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'PK_InboxMessages'
                )
                THEN
                    ALTER TABLE "InboxMessages"
                    ADD CONSTRAINT "PK_InboxMessages" PRIMARY KEY ("Id");
                END IF;
            END $$;
            """);

        Execute.Sql("""
            DO $$
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'InboxMessages'
                        AND column_name = 'Type'
                )
                THEN
                    ALTER TABLE "InboxMessages"
                    RENAME COLUMN "Type" TO "MessageType";
                END IF;
            END $$;
            """);

        Execute.Sql("""
            ALTER TABLE "InboxMessages"
            ADD COLUMN IF NOT EXISTS "ConsumerName" varchar(200) NOT NULL DEFAULT 'NotificationCore.RabbitMqNotificationConsumer',
            ADD COLUMN IF NOT EXISTS "RetryCount" integer NOT NULL DEFAULT 0,
            ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
            ADD COLUMN IF NOT EXISTS "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT now();
            """);

        Execute.Sql("""
            DO $$
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'InboxMessages'
                        AND column_name = 'Source'
                )
                THEN
                    ALTER TABLE "InboxMessages"
                    ALTER COLUMN "Source" SET DEFAULT 'Unknown';
                END IF;
            END $$;
            """);

        Execute.Sql("""
            UPDATE "InboxMessages"
            SET "Source" = 'Unknown'
            WHERE "Source" IS NULL OR btrim("Source") = '';
            """);

        Execute.Sql("""
            ALTER TABLE "InboxMessages"
            ALTER COLUMN "ConsumerName" DROP DEFAULT,
            ALTER COLUMN "RetryCount" DROP DEFAULT,
            ALTER COLUMN "CreatedAtUtc" DROP DEFAULT,
            ALTER COLUMN "UpdatedAtUtc" DROP DEFAULT;
            """);

        Execute.Sql("""
            DO $$
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'InboxMessages'
                        AND column_name = 'Source'
                )
                THEN
                    ALTER TABLE "InboxMessages"
                    ALTER COLUMN "Source" DROP DEFAULT;
                END IF;
            END $$;
            """);

        Execute.Sql("""
            DROP INDEX IF EXISTS "IX_InboxMessages_MessageId";

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_InboxMessages_MessageId_MessageType_ConsumerName"
            ON "InboxMessages" ("MessageId", "MessageType", "ConsumerName");

            CREATE INDEX IF NOT EXISTS "IX_InboxMessages_Status_CreatedAtUtc"
            ON "InboxMessages" ("Status", "CreatedAtUtc");

            CREATE INDEX IF NOT EXISTS "IX_InboxMessages_ProcessedAtUtc"
            ON "InboxMessages" ("ProcessedAtUtc");
            """);
    }
}
