using AuthCore.Api.Authentication;
using AuthCore.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa handler de autorizacao para sessoes ativas.
/// </summary>
public sealed class ActiveSessionAuthorizationHandler : AuthorizationHandler<ActiveSessionRequirement>
{
    /// <summary>
    /// Campo que armazena contexto da sessao autenticada.
    /// </summary>
    private readonly IAuthenticatedSessionContext _authenticatedSessionContext;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="authenticatedSessionContext">Contexto da sessao autenticada.</param>
    public ActiveSessionAuthorizationHandler(IAuthenticatedSessionContext authenticatedSessionContext)
    {
        ArgumentNullException.ThrowIfNull(authenticatedSessionContext);

        _authenticatedSessionContext = authenticatedSessionContext;
    }


    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveSessionRequirement requirement)
    {
        if (!_authenticatedSessionContext.IsActive)
        {
            context.Fail(new AuthorizationFailureReason(this, "O usuario nao pode autenticar no momento."));
            return Task.CompletedTask;
        }

        if (_authenticatedSessionContext.UserStatus == UserStatus.PendingEmailVerification)
        {
            context.Fail(new AuthorizationFailureReason(this, "O usuario precisa verificar o e-mail antes de autenticar."));
            return Task.CompletedTask;
        }

        if (_authenticatedSessionContext.UserStatus == UserStatus.Blocked)
        {
            context.Fail(new AuthorizationFailureReason(this, "O usuario esta bloqueado para autenticacao."));
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
