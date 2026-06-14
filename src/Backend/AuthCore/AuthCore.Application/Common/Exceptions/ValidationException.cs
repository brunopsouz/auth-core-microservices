using System.Net;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Application.Common.Exceptions;

/// <summary>
/// Representa exceção de aplicação para falha de validação.
/// </summary>
public sealed class ValidationException : AuthCoreException
{
    /// <summary>
    /// Obtém os erros de validação.
    /// </summary>
    public IReadOnlyCollection<string> Errors { get; }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    public ValidationException(string message) : base(message)
    {
        Errors = [message];
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="errors">Erros de validação encontrados.</param>
    public ValidationException(IEnumerable<string> errors) : base("Uma ou mais validações falharam.")
    {
        ArgumentNullException.ThrowIfNull(errors);

        Errors = errors.ToArray();
    }

    /// <summary>
    /// Operacao para obter as mensagens de erro.
    /// </summary>
    /// <returns>Mensagens de erro da excecao.</returns>
    public override IList<string> GetErrorMessages()
    {
        return Errors.ToList();
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
