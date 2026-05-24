using AuthCore.Domain.Common;

namespace AuthCore.Domain.Common;

/// <summary>
/// Representa o tipo base para raízes de agregado.
/// </summary>
public abstract class AggregateRoot : EntityBase
{

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador único da entidade.</param>
    protected AggregateRoot(Guid id) : base(id)
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador único da entidade.</param>
    /// <param name="createdAt">Data de criação persistida.</param>
    /// <param name="updateAt">Data da última atualização persistida.</param>
    /// <param name="isActive">Status de atividade persistido.</param>
    protected AggregateRoot(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive)
        : base(id, createdAt, updateAt, isActive)
    {
    }

}
