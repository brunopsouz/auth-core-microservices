using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Aggregates;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;

/// <summary>
/// Representa recuperacao de notificacoes com processamento expirado.
/// </summary>
internal sealed class TimedOutNotificationRecovery : ITimedOutNotificationRecovery
{
    private const string PROCESSING_TIMEOUT_PROVIDER = "NotificationDispatcher";
    private const string PROCESSING_TIMEOUT_CODE = "PROCESSING_TIMEOUT";
    private const string PROCESSING_TIMEOUT_MESSAGE = "Processamento anterior expirou.";

    /// <summary>
    /// Campo que armazena notification dispatch repository.
    /// </summary>
    private readonly INotificationDispatchRepository _notificationRepository;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="notificationRepository">Repositorio de despacho de notificacoes.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public TimedOutNotificationRecovery(
        INotificationDispatchRepository notificationRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Operacao para recuperar notificacoes com processamento expirado.
    /// </summary>
    /// <param name="command">Comando com os criterios do despacho.</param>
    /// <param name="counters">Contadores do processamento.</param>
    public async Task RecoverAsync(DispatchPendingNotificationCommand command, DispatchCounters counters)
    {
        var remaining = command.Take - counters.Found;

        if (remaining <= 0)
            return;

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var timedOutNotifications = await _notificationRepository.GetProcessingTimedOutAsync(command.DueAtUtc, remaining);
            counters.Found += timedOutNotifications.Count;

            foreach (var notification in timedOutNotifications)
            {
                var processingTimeoutAtUtc = notification.ScheduledAtUtc;

                MarkAsPermanentFailure(
                    notification,
                    DateTime.UtcNow);

                if (await _notificationRepository.TryUpdateProcessingTimedOutAsync(notification, processingTimeoutAtUtc))
                    counters.DeadLettered++;
            }

            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Operacao para marcar falha permanente por timeout.
    /// </summary>
    /// <param name="notification">Notificacao processada.</param>
    /// <param name="startedAtUtc">Data de inicio da tentativa.</param>
    private static void MarkAsPermanentFailure(Notification notification, DateTime startedAtUtc)
    {
        notification.RegisterPermanentFailureDeliveryAttempt(
            PROCESSING_TIMEOUT_PROVIDER,
            startedAtUtc,
            DateTime.UtcNow,
            PROCESSING_TIMEOUT_CODE,
            PROCESSING_TIMEOUT_MESSAGE);
    }
}
