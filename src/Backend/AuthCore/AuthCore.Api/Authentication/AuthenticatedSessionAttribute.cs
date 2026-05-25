using Microsoft.AspNetCore.Authorization;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa atributo para exigir sessão autenticada por cookie.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuthenticatedSessionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    public AuthenticatedSessionAttribute()
    {
        AuthenticationSchemes = SessionAuthenticationDefaults.AuthenticationScheme;
    }
}
