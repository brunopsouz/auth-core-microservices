using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.TABLE_USERS, "Create table to save the user's information")]
/// <summary>
/// Representa a migração de criação da tabela de usuários.
/// </summary>
public sealed class Version0000001 : VersionBase
{
    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        CreateTable("Users")
            .WithColumn("FirstName").AsString(100).NotNullable()
            .WithColumn("LastName").AsString(100).NotNullable()
            .WithColumn("FullName").AsString(200).NotNullable()
            .WithColumn("Email").AsString(320).NotNullable()
            .WithColumn("Contact").AsString(30).NotNullable()
            .WithColumn("UserIdentifier").AsGuid().NotNullable()
            .WithColumn("Role").AsInt32().NotNullable();

        Create.Index("IX_Users_Email")
            .OnTable("Users")
            .OnColumn("Email").Ascending()
            .WithOptions().Unique();

        Create.Index("IX_Users_UserIdentifier")
            .OnTable("Users")
            .OnColumn("UserIdentifier").Ascending()
            .WithOptions().Unique();
    }
}
