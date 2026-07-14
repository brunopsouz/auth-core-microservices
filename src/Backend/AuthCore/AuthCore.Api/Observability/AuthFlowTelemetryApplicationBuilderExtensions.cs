namespace AuthCore.Api.Observability;

/// <summary>
/// Define operações para configurar telemetria dos fluxos de autenticação.
/// </summary>
internal static class AuthFlowTelemetryApplicationBuilderExtensions
{
    /// <summary>
    /// Operação para adicionar telemetria dos fluxos de autenticação ao pipeline HTTP.
    /// </summary>
    /// <param name="app">Pipeline da aplicação.</param>
    /// <returns>Pipeline da aplicação atualizado.</returns>
    public static IApplicationBuilder UseAuthFlowTelemetry(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<AuthFlowTelemetryMiddleware>();
    }
}
