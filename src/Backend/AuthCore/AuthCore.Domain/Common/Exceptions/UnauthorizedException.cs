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

    /// <summary>
    /// Operacao para obter as mensagens de erro.
    /// </summary>
    /// <returns>Mensagens de erro da excecao.</returns>
    public override IList<string> GetErrorMessages()
    {
        return [Message];
    }

    /// <summary>
    /// Operacao para obter o status code HTTP.
    /// </summary>
    /// <returns>Status code HTTP da excecao.</returns>
    public override HttpStatusCode GetStatusCode()
    {
        return HttpStatusCode.Unauthorized;
    }
}
