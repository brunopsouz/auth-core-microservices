namespace Shared.Messaging.Contracts;

/// <summary>
/// Representa envelope técnico de transporte para mensagens assíncronas.
/// </summary>
/// <typeparam name="T">Tipo do payload de negócio.</typeparam>
public sealed class MessageEnvelope<T>
{
    /// <summary>
    /// Metadados técnicos de transporte.
    /// </summary>
    public MessageEnvelopeMetadata Metadata { get; init; } = new();

    /// <summary>
    /// Payload de negócio.
    /// </summary>
    public T? Payload { get; init; }
}
