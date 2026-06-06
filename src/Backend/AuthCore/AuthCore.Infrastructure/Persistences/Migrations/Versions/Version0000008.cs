using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.TABLE_OUTBOX_MESSAGES, "Create table to store outbox messages")]
/// <summary>
/// Representa a migração de criação da tabela de outbox.
/// </summary>
public sealed class Version0000008 : VersionBase
{
    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        CreateTable("OutboxMessages")
            .WithColumn("Type").AsString(200).NotNullable()
            .WithColumn("Content").AsString(int.MaxValue).NotNullable()
            .WithColumn("OccurredAtUtc").AsDateTime().NotNullable()
            .WithColumn("ProcessedAtUtc").AsDateTime().Nullable()
            .WithColumn("AttemptCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("LastError").AsString(2000).Nullable();

        Create.Index("IX_OutboxMessages_ProcessedAtUtc")
            .OnTable("OutboxMessages")
            .OnColumn("ProcessedAtUtc").Ascending();
    }
}
