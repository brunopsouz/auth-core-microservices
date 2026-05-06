namespace NotificationCore.Domain.Common.ValueObjects;

/// <summary>
/// Representa objeto de valor do domínio.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Operação para obter os componentes usados na comparação de igualdade.
    /// </summary>
    /// <returns>Componentes usados na comparação.</returns>
    protected abstract IEnumerable<object> GetEqualityComponents();

    /// <summary>
    /// Operação para comparar o objeto informado com a instância atual.
    /// </summary>
    /// <param name="obj">Objeto a ser comparado.</param>
    /// <returns>Verdadeiro quando os objetos são equivalentes.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other || other.GetType() != GetType())
            return false;

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// Operação para obter o código hash da instância atual.
    /// </summary>
    /// <returns>Código hash calculado a partir dos componentes de igualdade.</returns>
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(1, (current, obj) => HashCode.Combine(current, obj));
    }
}
