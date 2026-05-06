namespace NotificationCore.Domain.Common.Entities;

/// <summary>
/// Representa entidade base do domínio.
/// </summary>
public abstract class EntityBase
{
    /// <summary>
    /// Identificador único da entidade.
    /// </summary>
    public Guid Id { get; protected set; }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    protected EntityBase()
    {
        Id = Guid.NewGuid();
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador único da entidade.</param>
    protected EntityBase(Guid id)
    {
        Id = id;
    }

    /// <summary>
    /// Operação para comparar o objeto informado com a instância atual.
    /// </summary>
    /// <param name="obj">Objeto a ser comparado.</param>
    /// <returns>Verdadeiro quando os objetos possuem o mesmo identificador.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not EntityBase other || other.GetType() != GetType())
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Id.Equals(other.Id);
    }

    /// <summary>
    /// Operação para obter o código hash da entidade.
    /// </summary>
    /// <returns>Código hash calculado pelo identificador.</returns>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
