using System.Text.RegularExpressions;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Common.ValueObjects;

namespace NotificationCore.Domain.Notifications.ValueObjects;

/// <summary>
/// Representa chave de template validada.
/// </summary>
public sealed class TemplateKey : ValueObject
{
    /// <summary>
    /// Valor normalizado da chave.
    /// </summary>
    public string Value { get; } = null!;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="value">Valor da chave.</param>
    private TemplateKey(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private TemplateKey()
    {
    }

    #endregion

    /// <summary>
    /// Operação para criar chave de template.
    /// </summary>
    /// <param name="value">Chave informada.</param>
    /// <returns>Instância criada de <see cref="TemplateKey"/>.</returns>
    public static TemplateKey Create(string value)
    {
        Validate(value);
        return new TemplateKey(Normalize(value));
    }

    /// <summary>
    /// Operação para normalizar a chave.
    /// </summary>
    /// <param name="value">Chave informada.</param>
    /// <returns>Chave normalizada.</returns>
    public static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Operação para validar a chave.
    /// </summary>
    /// <param name="value">Chave informada.</param>
    public static void Validate(string value)
    {
        DomainException.When(string.IsNullOrWhiteSpace(value), "A chave do template é obrigatória.");

        var normalizedValue = Normalize(value);
        var regex = new Regex(@"^[a-z0-9][a-z0-9._-]{1,127}$");

        DomainException.When(!regex.IsMatch(normalizedValue), "A chave do template é inválida.");
    }

    /// <summary>
    /// Operação para retornar o valor textual.
    /// </summary>
    /// <returns>Valor textual da chave.</returns>
    public override string ToString()
    {
        return Value;
    }

    #region Helpers

    /// <summary>
    /// Operação para obter os componentes usados na igualdade.
    /// </summary>
    /// <returns>Componentes usados na comparação.</returns>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    #endregion
}
