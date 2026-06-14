using System.Net;

namespace AuthCore.Domain.Common.Exceptions;

/// <summary>
/// Representa exceção de acesso proibido para o contexto atual.
/// </summary>
public sealed class ForbiddenException : AuthCoreException
{
    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem descritiva da falha de autorização.</param>
    public ForbiddenException(string message) : base(message)
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
        return HttpStatusCode.Forbidden;
    }
}
