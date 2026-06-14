using System.Net;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Application.Common.Exceptions;

/// <summary>
/// Representa exceção de aplicação para verificação de e-mail inválida.
/// </summary>
public sealed class InvalidEmailVerificationException : AuthCoreException
{
    /// <summary>
    /// Mensagem pública padronizada para falha de verificação de e-mail.
    /// </summary>
    public const string InvalidVerificationMessage = "Não foi possível validar o código de verificação informado.";

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    public InvalidEmailVerificationException() : base(InvalidVerificationMessage)
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
        return HttpStatusCode.BadRequest;
    }
}
