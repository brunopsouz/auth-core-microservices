using NotificationCore.Domain.Common.Entities;

namespace NotificationCore.Domain.Common.Aggregates;

/// <summary>
/// Representa raiz de agregado do domínio.
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
}
