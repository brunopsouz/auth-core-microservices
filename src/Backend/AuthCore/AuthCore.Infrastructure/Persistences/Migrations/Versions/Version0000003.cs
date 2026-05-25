using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.USERS_EMAIL_VERIFICATION, "Add email verification support to users table")]
/// <summary>
/// Representa a migração de inclusão da verificação de e-mail.
/// </summary>
internal sealed class Version0000003 : ForwardOnlyMigration
{
    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        Alter.Table("Users")
            .AddColumn("EmailVerifiedAt").AsDateTime().Nullable();
    }
}
