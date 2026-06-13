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
}
