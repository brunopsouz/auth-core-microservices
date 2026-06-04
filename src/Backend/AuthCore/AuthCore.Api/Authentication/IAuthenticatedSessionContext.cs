using AuthCore.Domain.Users;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Define operacoes para acessar os dados da sessao autenticada.
/// </summary>
public interface IAuthenticatedSessionContext
{
    /// <summary>
    /// Identificador publico do usuario autenticado.
    /// </summary>
    Guid UserIdentifier { get; }

    /// <summary>
    /// Identificador interno do usuario autenticado.
    /// </summary>
    Guid InternalUserId { get; }

    /// <summary>
    /// E-mail do usuario autenticado.
    /// </summary>
    string Email { get; }

    /// <summary>
    /// Identificador opaco da sessao autenticada.
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Identificador publico da sessao autenticada.
    /// </summary>
    string PublicSessionId { get; }

    /// <summary>
    /// Status funcional do usuario autenticado.
    /// </summary>
    UserStatus UserStatus { get; }

    /// <summary>
    /// Indicacao de atividade do usuario autenticado.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Operacao para tentar obter o identificador publico do usuario autenticado.
    /// </summary>
    /// <param name="userIdentifier">Identificador publico do usuario autenticado.</param>
    /// <returns>Verdadeiro quando o identificador foi encontrado.</returns>
    bool TryGetUserIdentifier(out Guid userIdentifier);
}
