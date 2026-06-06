using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.EMAIL_VERIFICATION_EXPANSION, "Expand email verification tokens table")]
/// <summary>
/// Representa a migração de expansão da tabela de verificação de e-mail.
/// </summary>
public sealed class Version0000007 : ForwardOnlyMigration
{
    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        Alter.Table("EmailVerificationTokens")
            .AddColumn("AttemptCount").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("MaxAttempts").AsInt32().NotNullable().WithDefaultValue(5)
            .AddColumn("CooldownUntilUtc").AsDateTime().Nullable()
            .AddColumn("LastSentAtUtc").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
    }
}
