namespace AuthCore.Domain.Common.Exceptions;

public class DomainException : Exception
{
    /// <summary> 
    /// Inicializa uma nova instância da classe <see cref="DomainException"/> com a mensagem de erro especificada.
    /// </summary> 
    /// <param name="message">Mensagem que descreve o erro.</param>
    public DomainException(string message) : base(message) { }

    /// <summary>
    /// Inicializa uma nova instância da classe <see cref="DomainException"/> 
    /// com a mensagem de erro especificada e uma referência à exceção interna que causou esta exceção.
    /// </summary>
    /// <param name="message">Mensagem que descreve o erro.</param>
    /// <param name="inner">Exceção que é a causa da exceção atual.</param>
    public DomainException(string message, Exception inner) : base(message, inner) { }
    
    /// <summary>
    /// Método estático para validações de pré-condição.
    /// </summary>
    /// <param name="hasError">Boleano para identificar se existe erro.</param>
    /// <param name="errorMessage">Mensagem que descreve o erro.</param>
    public static void When(bool hasError, string errorMessage)
    {
        if(hasError)
        {   
            throw new DomainException(errorMessage);
        }
    }
};
