using FluentMigrator;
using FluentMigrator.Builders.Create.Table;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

/// <summary>
/// Representa a base para migrações versionadas do banco.
/// </summary>
public abstract class VersionBase : ForwardOnlyMigration
{
    /// <summary>
    /// Operação para criar uma tabela padrão com colunas compartilhadas.
    /// </summary>
    /// <param name="table">Nome da tabela.</param>
    protected ICreateTableColumnOptionOrWithColumnSyntax CreateTable(string table)
    {
        return Create.Table(table)
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdateAt").AsDateTime().NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true);
    }
}
