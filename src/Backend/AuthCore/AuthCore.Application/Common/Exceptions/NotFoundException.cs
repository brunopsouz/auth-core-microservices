namespace AuthCore.Application.Common.Exceptions;

/// <summary>
/// Representa exceção de aplicação para recurso não encontrado.
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    public NotFoundException(string message) : base(message) { }
}
