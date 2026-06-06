using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.USERS_STATUS, "Add user status to users table")]
/// <summary>
/// Representa a migração de inclusão do status funcional do usuário.
/// </summary>
public sealed class Version0000006 : ForwardOnlyMigration
{
    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        Alter.Table("Users")
            .AddColumn("Status").AsInt32().NotNullable().WithDefaultValue(0);

        Execute.Sql("""
            UPDATE "Users"
            SET "Status" =
                CASE
                    WHEN "IsActive" = FALSE THEN 2
                    WHEN "EmailVerifiedAt" IS NOT NULL THEN 1
                    ELSE 0
                END;
            """);
    }
}
