using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.USERS_SECURITY_STAMP, "Add security stamp to users")]
/// <summary>
/// Representa a migracao de inclusao do carimbo de seguranca do usuario.
/// </summary>
public sealed class Version0000009 : VersionBase
{
    /// <summary>
    /// Operacao para aplicar a migracao da versao atual.
    /// </summary>
    public override void Up()
    {
        Alter.Table("Users")
            .AddColumn("SecurityStamp").AsString(128).Nullable();

        Execute.Sql("""
            UPDATE "Users"
            SET "SecurityStamp" = md5(random()::text || clock_timestamp()::text || "Id"::text)
            WHERE "SecurityStamp" IS NULL;
            """);

        Alter.Column("SecurityStamp")
            .OnTable("Users")
            .AsString(128)
            .NotNullable();
    }
}
