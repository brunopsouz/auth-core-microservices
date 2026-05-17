using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Gateway.Api;

/// <summary>
/// Define operações para configurar o pipeline HTTP do Gateway.
/// </summary>
public static class GatewayApplicationBuilderExtensions
{
    private const string RateLimitClientIdHeader = "X-Client-Id";
    private const string UnknownClientId = "unknown-client";

    /// <summary>
    /// Operação para adicionar identificação do cliente usada pelo rate limit.
    /// </summary>
    /// <param name="app">Construtor do pipeline HTTP.</param>
    /// <returns>Construtor do pipeline HTTP atualizado.</returns>
    public static IApplicationBuilder UseGatewayRateLimitClientIdentity(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var clientId = GetClientId(context);

            context.Request.Headers[RateLimitClientIdHeader] = new StringValues(clientId);

            await next();
        });
    }

    private static string GetClientId(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(userId))
            return $"user:{userId}";

        var remoteIpAddress = context.Connection.RemoteIpAddress?.ToString();

        if (!string.IsNullOrWhiteSpace(remoteIpAddress))
            return $"ip:{remoteIpAddress}";

        return UnknownClientId;
    }
}
