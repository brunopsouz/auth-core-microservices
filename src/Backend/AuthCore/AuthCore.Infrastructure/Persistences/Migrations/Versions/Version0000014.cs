using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(
    DatabaseVersions.EXTERNAL_LOGINS_USER_PROVIDER_UNIQUENESS,
    "Enforce one external login per user and provider")]
/// <summary>
/// Representa a migracao de unicidade do provedor externo por usuario.
/// </summary>
public sealed class Version0000014 : ForwardOnlyMigration
{
    /// <summary>
    /// Operacao para aplicar a migracao da versao atual.
    /// </summary>
    public override void Up()
    {
        Delete.Index("IX_external_logins_user_id")
            .OnTable("external_logins");

        Create.Index("UX_external_logins_user_id_provider")
            .OnTable("external_logins")
            .OnColumn("user_id").Ascending()
            .OnColumn("provider").Ascending()
            .WithOptions().Unique();
    }
}
