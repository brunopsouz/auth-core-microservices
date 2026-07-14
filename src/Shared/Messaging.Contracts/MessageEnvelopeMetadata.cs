namespace Shared.Messaging.Contracts;

/// <summary>
/// Representa metadados técnicos de transporte de mensagem.
/// </summary>
public sealed class MessageEnvelopeMetadata
{
    /// <summary>
    /// Identificador de correlação do fluxo.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Header W3C traceparent.
    /// </summary>
    public string? TraceParent { get; init; }

    /// <summary>
    /// Header W3C tracestate.
    /// </summary>
    public string? TraceState { get; init; }
}
