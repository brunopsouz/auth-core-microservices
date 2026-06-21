using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.AUTH_SESSIONS_VERSION, "Add monotonic version to durable auth sessions")]
/// <summary>
/// Representa a migracao de inclusao da versao monotonicamente crescente das sessoes.
/// </summary>
public sealed class Version0000013 : ForwardOnlyMigration
{
    /// <summary>
    /// Operacao para aplicar a migracao da versao atual.
    /// </summary>
    public override void Up()
    {
        Alter.Table("auth_sessions")
            .AddColumn("version").AsInt64().NotNullable().WithDefaultValue(1L);
    }
}
