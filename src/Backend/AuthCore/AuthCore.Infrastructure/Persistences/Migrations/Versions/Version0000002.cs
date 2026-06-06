using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.TABLE_PASSWORDS, "Create table to save user passwords and login attempts")]
/// <summary>
/// Representa a migração de criação da tabela de senhas.
/// </summary>
public sealed class Version0000002 : ForwardOnlyMigration
{
    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        Create.Table("Passwords")
            .WithColumn("UserId").AsGuid().PrimaryKey()
            .WithColumn("Value").AsString(512).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("FailedAttempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("LastFailedAt").AsDateTime().Nullable()
            .WithColumn("LockedUntil").AsDateTime().Nullable();

        Create.ForeignKey("FK_Passwords_Users_UserId")
            .FromTable("Passwords").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);
    }
}
