namespace AuthCore.Domain.Common.Exceptions;

/// <summary>
/// Representa exceção para conflito de negócio.
/// </summary>
public sealed class ConflictException : DomainException
{

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    public ConflictException(string message) : base(message) { }

}
