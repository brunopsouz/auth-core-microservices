using System.Data;
using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.TABLE_EXTERNAL_LOGINS, "Create table to store external logins")]
/// <summary>
/// Representa a migracao de criacao da tabela de logins externos.
/// </summary>
public sealed class Version0000011 : ForwardOnlyMigration
{
    /// <summary>
    /// Operacao para aplicar a migracao da versao atual.
    /// </summary>
    public override void Up()
    {
        Create.Table("external_logins")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("update_at").AsDateTime().NotNullable()
            .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("user_id").AsGuid().NotNullable()
            .WithColumn("provider").AsInt16().NotNullable()
            .WithColumn("provider_user_id").AsString(255).NotNullable()
            .WithColumn("email").AsString(320).NotNullable()
            .WithColumn("email_verified").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("linked_at_utc").AsDateTime().NotNullable()
            .WithColumn("last_used_at_utc").AsDateTime().Nullable();

        Create.Index("UX_external_logins_provider_provider_user_id")
            .OnTable("external_logins")
            .OnColumn("provider").Ascending()
            .OnColumn("provider_user_id").Ascending()
            .WithOptions().Unique();

        Create.Index("IX_external_logins_user_id")
            .OnTable("external_logins")
            .OnColumn("user_id").Ascending();

        Create.ForeignKey("FK_external_logins_Users_user_id")
            .FromTable("external_logins").ForeignColumn("user_id")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDeleteOrUpdate(Rule.Cascade);
    }
}
