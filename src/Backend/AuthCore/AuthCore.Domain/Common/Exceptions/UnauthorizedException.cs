using System.Net;

namespace AuthCore.Domain.Common.Exceptions;

/// <summary>
/// Representa exceção de acesso não autorizado.
/// </summary>
public sealed class UnauthorizedException : AuthCoreException
{
    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    public UnauthorizedException(string message) : base(message)
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
        return HttpStatusCode.Unauthorized;
    }
}
