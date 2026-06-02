using System.Security.Cryptography;
using AuthCore.Domain.Common;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Domain.Users;

/// <summary>
/// Representa o carimbo de seguranca do usuario.
/// </summary>
public sealed class SecurityStamp : ValueObject
{
    /// <summary>
    /// Valor do carimbo de seguranca.
    /// </summary>
    public string Value { get; } = string.Empty;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    private SecurityStamp()
    {
    }

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="value">Valor do carimbo de seguranca.</param>
    private SecurityStamp(string value)
    {
        Value = value;
    }


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <returns>Carimbo de seguranca criado.</returns>
    public static SecurityStamp Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var value = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new SecurityStamp(value);
    }

    /// <summary>
    /// Operacao para restaurar instancia da classe.
    /// </summary>
    /// <param name="value">Valor persistido do carimbo de seguranca.</param>
    /// <returns>Carimbo de seguranca restaurado.</returns>
    public static SecurityStamp Restore(string value)
    {
        var normalizedValue = Normalize(value);

        DomainException.When(string.IsNullOrWhiteSpace(normalizedValue), "O carimbo de seguranca do usuario e obrigatorio.");

        return new SecurityStamp(normalizedValue);
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
    /// Operacao para normalizar o carimbo de seguranca.
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
