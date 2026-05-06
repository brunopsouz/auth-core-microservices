using System.Text.RegularExpressions;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Common.ValueObjects;

namespace NotificationCore.Domain.Notifications.ValueObjects;

/// <summary>
/// Representa e-mail de destinatário validado.
/// </summary>
public sealed class RecipientEmail : ValueObject
{
    /// <summary>
    /// Valor normalizado do e-mail.
    /// </summary>
    public string Value { get; } = null!;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="value">Valor do e-mail.</param>
    private RecipientEmail(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private RecipientEmail()
    {
    }

    #endregion

    /// <summary>
    /// Operação para criar e-mail de destinatário.
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    /// <returns>Instância criada de <see cref="RecipientEmail"/>.</returns>
    public static RecipientEmail Create(string email)
    {
        Validate(email);
        return new RecipientEmail(Normalize(email));
    }

    /// <summary>
    /// Operação para normalizar o e-mail.
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    /// <returns>E-mail normalizado.</returns>
    public static string Normalize(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Operação para validar o e-mail.
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    public static void Validate(string email)
    {
        DomainException.When(string.IsNullOrWhiteSpace(email), "O e-mail do destinatário é obrigatório.");

        var regex = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");

        DomainException.When(!regex.IsMatch(Normalize(email)), "O e-mail do destinatário é inválido.");
    }

    /// <summary>
    /// Operação para retornar o valor textual.
    /// </summary>
    /// <returns>Valor textual do e-mail.</returns>
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
