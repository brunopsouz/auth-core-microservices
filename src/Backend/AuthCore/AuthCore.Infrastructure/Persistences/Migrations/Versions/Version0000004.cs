using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.TABLE_EMAIL_VERIFICATION_TOKENS, "Create table to store email verification tokens")]
/// <summary>
/// Representa a migração de criação da tabela de tokens de verificação de e-mail.
/// </summary>
public sealed class Version0000004 : VersionBase
{
    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        CreateTable("EmailVerificationTokens")
            .WithColumn("UserId").AsGuid().NotNullable()
            .WithColumn("Email").AsString(320).NotNullable()
            .WithColumn("TokenHash").AsString(128).NotNullable()
            .WithColumn("ExpiresAtUtc").AsDateTime().NotNullable()
            .WithColumn("ConsumedAtUtc").AsDateTime().Nullable()
            .WithColumn("RevokedAtUtc").AsDateTime().Nullable();

        Create.Index("IX_EmailVerificationTokens_UserId")
            .OnTable("EmailVerificationTokens")
            .OnColumn("UserId").Ascending();

        Create.Index("IX_EmailVerificationTokens_TokenHash")
            .OnTable("EmailVerificationTokens")
            .OnColumn("TokenHash").Ascending()
            .WithOptions().Unique();

        Create.ForeignKey("FK_EmailVerificationTokens_Users_UserId")
            .FromTable("EmailVerificationTokens").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);
    }
}
