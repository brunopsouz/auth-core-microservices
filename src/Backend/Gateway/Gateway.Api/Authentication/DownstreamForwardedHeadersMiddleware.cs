using Shared.Observability;

namespace Gateway.Api.Authentication;

/// <summary>
/// Representa middleware para encaminhar host e protocolo publicos ao downstream.
/// </summary>
internal sealed class DownstreamForwardedHeadersMiddleware
{
    private const string XForwardedHostHeaderName = "X-Forwarded-Host";
    private const string XForwardedProtoHeaderName = "X-Forwarded-Proto";
    private const string TraceParentHeaderName = "traceparent";
    private const string TraceStateHeaderName = "tracestate";
    private const string BaggageHeaderName = "baggage";

    private readonly RequestDelegate _next;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="next">Proximo middleware do pipeline.</param>
    public DownstreamForwardedHeadersMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>
    /// Operacao para anexar os valores normalizados pelo Gateway.
    /// </summary>
    /// <param name="context">Contexto HTTP atual.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Request.Headers[XForwardedHostHeaderName] = context.Request.Host.Value;
        context.Request.Headers[XForwardedProtoHeaderName] = context.Request.Scheme;
        context.Request.Headers.Remove(TraceParentHeaderName);
        context.Request.Headers.Remove(TraceStateHeaderName);
        context.Request.Headers.Remove(BaggageHeaderName);

        if (context.Items.TryGetValue(CorrelationIdConstants.HttpContextItemKey, out var value)
            && value is string correlationId)
        {
            context.Request.Headers[CorrelationIdConstants.HeaderName] = correlationId;
        }

        await _next(context);
    }
}
