using Microsoft.AspNetCore.Builder;

namespace Shared.Observability;

/// <summary>
/// Representa extensões para configurar o identificador de correlação HTTP.
/// </summary>
public static class CorrelationIdApplicationBuilderExtensions
{
    /// <summary>
    /// Operação para adicionar o middleware de correlação HTTP ao pipeline.
    /// </summary>
    /// <param name="app">Construtor do pipeline HTTP.</param>
    /// <returns>Construtor do pipeline HTTP atualizado.</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
