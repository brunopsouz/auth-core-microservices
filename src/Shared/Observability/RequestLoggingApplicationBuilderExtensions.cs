using Microsoft.AspNetCore.Builder;

namespace Shared.Observability;

/// <summary>
/// Representa extensões para configurar o log de requisições HTTP.
/// </summary>
public static class RequestLoggingApplicationBuilderExtensions
{
    /// <summary>
    /// Operação para adicionar o middleware de log de conclusão HTTP ao pipeline.
    /// </summary>
    /// <param name="app">Construtor do pipeline HTTP.</param>
    /// <returns>Construtor do pipeline HTTP atualizado.</returns>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
