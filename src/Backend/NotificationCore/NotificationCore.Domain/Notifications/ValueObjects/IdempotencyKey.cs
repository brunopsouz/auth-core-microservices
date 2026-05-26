using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Common.ValueObjects;

namespace NotificationCore.Domain.Notifications.ValueObjects;

/// <summary>
/// Representa chave de idempotência validada.
/// </summary>
public sealed class IdempotencyKey : ValueObject
{
    /// <summary>
    /// Tamanho máximo da chave de idempotência.
    /// </summary>
    private const int MAX_LENGTH = 200;

    /// <summary>
    /// Valor normalizado da chave.
    /// </summary>
    public string Value { get; } = null!;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="value">Valor da chave.</param>
    private IdempotencyKey(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private IdempotencyKey()
    {
    }


    /// <summary>
    /// Operação para criar chave de idempotência.
    /// </summary>
    /// <param name="value">Chave informada.</param>
    /// <returns>Instância criada de <see cref="IdempotencyKey"/>.</returns>
    public static IdempotencyKey Create(string value)
    {
        Validate(value);
        return new IdempotencyKey(Normalize(value));
    }

    /// <summary>
    /// Operação para normalizar a chave.
    /// </summary>
    /// <param name="value">Chave informada.</param>
    /// <returns>Chave normalizada.</returns>
    public static string Normalize(string value)
    {
        return value.Trim();
    }

    /// <summary>
    /// Operação para validar a chave.
    /// </summary>
    /// <param name="value">Chave informada.</param>
    public static void Validate(string value)
    {
        DomainException.When(string.IsNullOrWhiteSpace(value), "A chave de idempotência é obrigatória.");
        DomainException.When(Normalize(value).Length > MAX_LENGTH, "A chave de idempotência excede o tamanho permitido.");
    }

    /// <summary>
    /// Operação para retornar o valor textual.
    /// </summary>
    /// <returns>Valor textual da chave.</returns>
    public override string ToString()
    {
        return Value;
    }


    /// <summary>
    /// Operação para obter os componentes usados na igualdade.
    /// </summary>
    /// <returns>Componentes usados na comparação.</returns>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

}
