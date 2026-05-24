namespace AuthCore.Domain.Common;

/// <summary>
/// Representa o tipo base para objetos de valor.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Operação para obter os componentes usados na comparação de igualdade.
    /// </summary>
    /// <returns>Componentes utilizados na comparação de igualdade.</returns>
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
    /// <returns>Código hash calculado para a instância.</returns>
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(1, (current, obj) => HashCode.Combine(current, obj));
    }
}
