using Microsoft.AspNetCore.Authorization;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa requisito de autorizacao para sessoes ativas.
/// </summary>
public sealed class ActiveSessionRequirement : IAuthorizationRequirement
{
}
