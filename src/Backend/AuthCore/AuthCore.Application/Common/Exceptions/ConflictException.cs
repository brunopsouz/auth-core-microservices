using System.Net;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Application.Common.Exceptions;

/// <summary>
/// Representa exceção de aplicação para conflito na execução do caso de uso.
/// </summary>
public sealed class ConflictException : AuthCoreException
{
    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    public ConflictException(string message) : base(message)
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
        return HttpStatusCode.Conflict;
    }
}
