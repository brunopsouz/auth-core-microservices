using System.Text.RegularExpressions;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common;

namespace AuthCore.Domain.Users;

/// <summary>
/// Representa um endereço de e-mail validado do domínio.
/// </summary>
public sealed class Email : ValueObject
{
    /// <summary>
    /// Valor normalizado do e-mail.
    /// </summary>
    public string Value { get; } = null!;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="value">Valor do e-mail.</param>
    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private Email()
    {
    }


    /// <summary>
    /// Operação para criar o e-mail validado.
    /// </summary>
    /// <param name="email">Endereço de e-mail informado.</param>
    /// <returns>Instância criada de <see cref="Email"/>.</returns>
    public static Email Create(string email)
    {
        Validate(email);
        return new Email(Normalize(email));
    }

    /// <summary>
    /// Operação para mascarar parte do endereço de e-mail.
    /// </summary>
    /// <returns>E-mail mascarado.</returns>
    public string Mask()
    {
        var parts = Value.Split('@');
        var local = parts[0];
        var domain = parts[1];

        if (local.Length <= 2)
            return $"{local[0]}***@{domain}";

        var visible = local[..2];
        return $"{visible}***@{domain}";
    }

    /// <summary>
    /// Operação para retornar o e-mail sem máscara.
    /// </summary>
    /// <returns>E-mail completo.</returns>
    public string Unmask()
    {
        return Value;
    }

    /// <summary>
    /// Operação para normalizar o e-mail.
    /// </summary>
    /// <param name="email">E-mail a normalizar.</param>
    /// <returns>E-mail normalizado.</returns>
    public static string Normalize(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Operação para validar o e-mail.
    /// </summary>
    /// <param name="email">E-mail a validar.</param>
    public static void Validate(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("O e-mail não pode estar vazio.");

        var regex = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");

        if (!regex.IsMatch(Normalize(email)))
            throw new DomainException("O e-mail informado é inválido.");
    }

    /// <summary>
    /// Operação para comparar este e-mail com outro objeto.
    /// </summary>
    /// <param name="obj">Objeto a ser comparado.</param>
    /// <returns><c>true</c> quando os valores são equivalentes.</returns>
    public override bool Equals(object? obj)
    {
        return base.Equals(obj);
    }

    /// <summary>
    /// Operação para obter o hash code do e-mail.
    /// </summary>
    /// <returns>Código hash baseado no valor normalizado.</returns>
    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    /// <summary>
    /// Operação para comparar dois e-mails.
    /// </summary>
    /// <param name="left">Primeiro e-mail.</param>
    /// <param name="right">Segundo e-mail.</param>
    /// <returns><c>true</c> quando os valores são equivalentes.</returns>
    public static bool operator ==(Email? left, Email? right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Operação para comparar dois e-mails.
    /// </summary>
    /// <param name="left">Primeiro e-mail.</param>
    /// <param name="right">Segundo e-mail.</param>
    /// <returns><c>true</c> quando os valores são diferentes.</returns>
    public static bool operator !=(Email? left, Email? right)
    {
        return !Equals(left, right);
    }


    /// <summary>
    /// Operação para obter os componentes usados na igualdade.
    /// </summary>
    /// <returns>Componentes utilizados na comparação.</returns>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

}
