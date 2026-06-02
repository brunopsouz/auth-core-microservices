using AuthCore.Domain.Common;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa o identificador opaco e secreto de uma sessao.
/// </summary>
public sealed class SessionIdentifier : ValueObject
{
    /// <summary>
    /// Valor do identificador opaco.
    /// </summary>
    public string Value { get; } = string.Empty;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    private SessionIdentifier()
    {
    }

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="value">Valor do identificador opaco.</param>
    private SessionIdentifier(string value)
    {
        Value = value;
    }


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="value">Valor do identificador opaco.</param>
    /// <returns>Identificador criado.</returns>
    public static SessionIdentifier Create(string value)
    {
        var normalizedValue = Normalize(value);

        DomainException.When(string.IsNullOrWhiteSpace(normalizedValue), "O identificador opaco da sessao e obrigatorio.");

        return new SessionIdentifier(normalizedValue);
    }

    /// <summary>
    /// Operacao para obter os componentes usados na igualdade.
    /// </summary>
    /// <returns>Componentes utilizados na comparacao.</returns>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Operacao para normalizar o identificador opaco.
    /// </summary>
    /// <param name="value">Valor informado.</param>
    /// <returns>Valor normalizado.</returns>
    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
