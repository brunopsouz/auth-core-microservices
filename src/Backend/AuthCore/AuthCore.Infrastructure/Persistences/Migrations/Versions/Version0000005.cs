using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.TABLE_REFRESH_TOKENS, "Create table to store refresh tokens")]
/// <summary>
/// Representa a migração de criação da tabela de refresh tokens.
/// </summary>
public sealed class Version0000005 : VersionBase
{
    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        CreateTable("RefreshTokens")
            .WithColumn("UserId").AsGuid().NotNullable()
            .WithColumn("FamilyId").AsGuid().NotNullable()
            .WithColumn("ParentTokenId").AsGuid().Nullable()
            .WithColumn("ReplacedByTokenId").AsGuid().Nullable()
            .WithColumn("TokenHash").AsString(128).NotNullable()
            .WithColumn("ExpiresAtUtc").AsDateTime().NotNullable()
            .WithColumn("ConsumedAtUtc").AsDateTime().Nullable()
            .WithColumn("RevokedAtUtc").AsDateTime().Nullable()
            .WithColumn("RevocationReason").AsString(200).Nullable();

        Create.Index("IX_RefreshTokens_UserId")
            .OnTable("RefreshTokens")
            .OnColumn("UserId").Ascending();

        Create.Index("IX_RefreshTokens_FamilyId")
            .OnTable("RefreshTokens")
            .OnColumn("FamilyId").Ascending();

        Create.Index("IX_RefreshTokens_TokenHash")
            .OnTable("RefreshTokens")
            .OnColumn("TokenHash").Ascending()
            .WithOptions().Unique();

        Create.Index("IX_RefreshTokens_ParentTokenId")
            .OnTable("RefreshTokens")
            .OnColumn("ParentTokenId").Ascending();

        Create.Index("IX_RefreshTokens_ReplacedByTokenId")
            .OnTable("RefreshTokens")
            .OnColumn("ReplacedByTokenId").Ascending();

        Create.ForeignKey("FK_RefreshTokens_Users_UserId")
            .FromTable("RefreshTokens").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);
    }
}
