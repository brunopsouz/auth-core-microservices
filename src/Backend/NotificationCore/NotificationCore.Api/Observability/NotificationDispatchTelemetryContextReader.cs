using System.Text.Json;
using NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Api.Observability;

/// <summary>
/// Representa leitor técnico de contexto seguro para telemetria de despacho.
/// </summary>
internal sealed class NotificationDispatchTelemetryContextReader
{
    private readonly IInboxRepository _inboxRepository;
    private readonly INotificationDispatchRepository _notificationRepository;
    private readonly ILogger<NotificationDispatchTelemetryContextReader> _logger;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="notificationRepository">Repositório de despacho de notificações.</param>
    /// <param name="inboxRepository">Repositório de inbox.</param>
    /// <param name="logger">Serviço de logging.</param>
    public NotificationDispatchTelemetryContextReader(
        INotificationDispatchRepository notificationRepository,
        IInboxRepository inboxRepository,
        ILogger<NotificationDispatchTelemetryContextReader> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);
        ArgumentNullException.ThrowIfNull(inboxRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _notificationRepository = notificationRepository;
        _inboxRepository = inboxRepository;
        _logger = logger;
    }

    /// <summary>
    /// Operação para obter contextos técnicos do próximo despacho.
    /// </summary>
    /// <param name="command">Comando de despacho.</param>
    /// <returns>Contextos técnicos seguros.</returns>
    public async Task<IReadOnlyCollection<NotificationDispatchTelemetryContext>> ReadAsync(
        DispatchPendingNotificationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            var notifications = await _notificationRepository.GetPendingForDispatchAsync(
                command.DueAtUtc,
                command.Take,
                command.CancellationToken);
            var contexts = new List<NotificationDispatchTelemetryContext>(notifications.Count);

            foreach (var notification in notifications)
            {
                var payload = await _inboxRepository.GetPayloadByNotificationIdempotencyKeyAsync(
                    notification.IdempotencyKey.Value,
                    command.CancellationToken);
                var traceContext = ReadTraceContext(payload);

                contexts.Add(new NotificationDispatchTelemetryContext
                {
                    TemplateKey = notification.TemplateKey.Value,
                    Channel = notification.Channel,
                    IsRetry = notification.DeliveryAttempts.Count > 0,
                    TraceParent = traceContext.TraceParent,
                    TraceState = traceContext.TraceState
                });
            }

            return contexts;
        }
        catch (OperationCanceledException) when (command.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Não foi possível carregar contexto técnico de telemetria do despacho.");

            return [];
        }
    }

    private static NotificationDispatchTraceContext ReadTraceContext(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return NotificationDispatchTraceContext.Empty;

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("Metadata", out var metadata))
                return NotificationDispatchTraceContext.Empty;

            var traceParent = TryGetString(metadata, "TraceParent");
            var traceState = TryGetString(metadata, "TraceState");

            return new NotificationDispatchTraceContext(traceParent, traceState);
        }
        catch (JsonException)
        {
            return NotificationDispatchTraceContext.Empty;
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private sealed record NotificationDispatchTraceContext(string? TraceParent, string? TraceState)
    {
        public static NotificationDispatchTraceContext Empty { get; } = new(null, null);
    }
}
