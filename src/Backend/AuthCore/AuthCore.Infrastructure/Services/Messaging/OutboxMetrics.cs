using System.Diagnostics.Metrics;

namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa métricas do processamento da outbox.
/// </summary>
internal sealed class OutboxMetrics
{
    private static readonly Meter Meter = new("AuthCore.Outbox", "1.0.0");

    private readonly Counter<long> _processedMessages = Meter.CreateCounter<long>(
        "authcore.outbox.messages.processed");
    private readonly Counter<long> _failedMessages = Meter.CreateCounter<long>(
        "authcore.outbox.messages.failed");
    private readonly Histogram<double> _processingDuration = Meter.CreateHistogram<double>(
        "authcore.outbox.processing.duration.ms");

    /// <summary>
    /// Operação para registrar mensagem processada.
    /// </summary>
    /// <param name="type">Tipo lógico da mensagem.</param>
    public void RecordProcessed(string type)
    {
        _processedMessages.Add(1, new KeyValuePair<string, object?>("type", type));
    }

    /// <summary>
    /// Operação para registrar mensagem com falha.
    /// </summary>
    /// <param name="type">Tipo lógico da mensagem.</param>
    public void RecordFailed(string type)
    {
        _failedMessages.Add(1, new KeyValuePair<string, object?>("type", type));
    }

    /// <summary>
    /// Operação para registrar duração do ciclo.
    /// </summary>
    /// <param name="elapsed">Duração do processamento.</param>
    public void RecordDuration(TimeSpan elapsed)
    {
        _processingDuration.Record(elapsed.TotalMilliseconds);
    }
}
