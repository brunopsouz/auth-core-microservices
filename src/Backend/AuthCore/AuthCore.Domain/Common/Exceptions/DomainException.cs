using System.Net;

namespace AuthCore.Domain.Common.Exceptions;

/// <summary>
/// Representa exceção de regra de negócio do domínio.
/// </summary>
public class DomainException : AuthCoreException
{
    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    public DomainException(string message) : base(message)
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    /// <param name="inner">Exceção que originou o erro.</param>
    public DomainException(string message, Exception inner) : base(message, inner)
    {
    }

    /// <inheritdoc />
    public override IList<string> GetErrorMessages()
    {
        return [Message];
    }

    /// <inheritdoc />
    public override HttpStatusCode GetStatusCode()
    {
        return HttpStatusCode.BadRequest;
    }

    /// <summary>
    /// Operação para validar pré-condição de domínio.
    /// </summary>
    /// <param name="hasError">Indica se existe erro.</param>
    /// <param name="errorMessage">Mensagem que descreve o erro.</param>
    public static void When(bool hasError, string errorMessage)
    {
        if (hasError)
            throw new DomainException(errorMessage);
    }
}
