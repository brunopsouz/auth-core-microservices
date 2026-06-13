using System.Net;

namespace AuthCore.Domain.Common.Exceptions;

/// <summary>
/// Representa exceção base conhecida do AuthCore.
/// </summary>
public abstract class AuthCoreException : SystemException
{
    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    protected AuthCoreException(string message) : base(message)
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    /// <param name="inner">Exceção que originou o erro.</param>
    protected AuthCoreException(string message, Exception inner) : base(message, inner)
    {
    }

    /// <summary>
    /// Operação para obter as mensagens de erro.
    /// </summary>
    /// <returns>Mensagens de erro da exceção.</returns>
    public abstract IList<string> GetErrorMessages();

    /// <summary>
    /// Operação para obter o status code HTTP.
    /// </summary>
    /// <returns>Status code HTTP da exceção.</returns>
    public abstract HttpStatusCode GetStatusCode();
}
