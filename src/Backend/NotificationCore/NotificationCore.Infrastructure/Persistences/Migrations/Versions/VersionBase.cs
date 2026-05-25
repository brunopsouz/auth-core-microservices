using FluentMigrator;
using FluentMigrator.Builders.Create.Table;

namespace NotificationCore.Infrastructure.Persistences.Migrations.Versions;

/// <summary>
/// Representa a base para migrações versionadas do banco.
/// </summary>
internal abstract class VersionBase : ForwardOnlyMigration
{
    /// <summary>
    /// Operação para criar uma tabela com identificador padrão.
    /// </summary>
    /// <param name="table">Nome da tabela.</param>
    /// <returns>Sintaxe para continuar a criação da tabela.</returns>
    protected ICreateTableColumnOptionOrWithColumnSyntax CreateTableWithId(string table)
    {
        return Create.Table(table)
            .WithColumn("Id").AsGuid().PrimaryKey();
    }
}
