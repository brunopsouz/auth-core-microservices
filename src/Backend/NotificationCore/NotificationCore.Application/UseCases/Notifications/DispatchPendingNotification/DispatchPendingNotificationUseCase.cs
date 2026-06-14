using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Common.Repositories;
using NotificationCore.Domain.Notifications.Providers;
using NotificationCore.Domain.Notifications.Rendering;
using NotificationCore.Domain.Notifications.Repositories;

namespace NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;

/// <summary>
/// Representa caso de uso para despachar notificacoes pendentes.
/// </summary>
internal sealed class DispatchPendingNotificationUseCase : IDispatchPendingNotificationUseCase
{
    /// <summary>
    /// Campo que armazena pending notification dispatcher.
    /// </summary>
    private readonly IPendingNotificationDispatcher _pendingNotificationDispatcher;
    /// <summary>
    /// Campo que armazena timed out notification recovery.
    /// </summary>
    private readonly ITimedOutNotificationRecovery _timedOutNotificationRecovery;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="timedOutNotificationRecovery">Recuperacao de notificacoes expiradas.</param>
    /// <param name="pendingNotificationDispatcher">Despachante de notificacoes pendentes.</param>
    internal DispatchPendingNotificationUseCase(
        ITimedOutNotificationRecovery timedOutNotificationRecovery,
        IPendingNotificationDispatcher pendingNotificationDispatcher)
    {
        ArgumentNullException.ThrowIfNull(timedOutNotificationRecovery);
        ArgumentNullException.ThrowIfNull(pendingNotificationDispatcher);

        _timedOutNotificationRecovery = timedOutNotificationRecovery;
        _pendingNotificationDispatcher = pendingNotificationDispatcher;
    }

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="notificationRepository">Repositorio de despacho de notificacoes.</param>
    /// <param name="inboxRepository">Repositorio de inbox.</param>
    /// <param name="templateRenderer">Renderizador de templates.</param>
    /// <param name="emailProvider">Provedor de e-mail.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public DispatchPendingNotificationUseCase(
        INotificationDispatchRepository notificationRepository,
        IInboxRepository inboxRepository,
        ITemplateRenderer templateRenderer,
        IEmailProvider emailProvider,
        IUnitOfWork unitOfWork)
        : this(
            new TimedOutNotificationRecovery(notificationRepository, unitOfWork),
            new PendingNotificationDispatcher(
                notificationRepository,
                ResolveWriterRepository(notificationRepository),
                inboxRepository,
                templateRenderer,
                emailProvider,
                unitOfWork))
    {
    }

    /// <summary>
    /// Operacao para despachar notificacoes pendentes.
    /// </summary>
    /// <param name="command">Comando com os criterios de despacho.</param>
    /// <returns>Resultado do despacho.</returns>
    public async Task<DispatchPendingNotificationResult> Execute(DispatchPendingNotificationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command);

        var counters = new DispatchCounters(found: 0);

        await _timedOutNotificationRecovery.RecoverAsync(command, counters);
        await _pendingNotificationDispatcher.DispatchAsync(command, counters);

        return counters.ToResult();
    }

    /// <summary>
    /// Operacao para validar o comando.
    /// </summary>
    /// <param name="command">Comando informado.</param>
    private static void Validate(DispatchPendingNotificationCommand command)
    {
        DomainException.When(command.DueAtUtc == default || command.DueAtUtc.Kind != DateTimeKind.Utc, "A data limite de despacho e obrigatoria e deve estar em UTC.");
        DomainException.When(command.Take <= 0, "A quantidade de notificacoes para despacho deve ser maior que zero.");
        DomainException.When(command.RetryDelay <= TimeSpan.Zero, "O intervalo de retry deve ser maior que zero.");
        DomainException.When(command.ProcessingTimeout <= TimeSpan.Zero, "O intervalo de processamento deve ser maior que zero.");
    }

    /// <summary>
    /// Operacao para resolver repositorio de escrita usado pela sobrecarga de compatibilidade.
    /// </summary>
    /// <param name="notificationRepository">Repositorio de despacho.</param>
    /// <returns>Repositorio de escrita.</returns>
    private static INotificationWriterRepository ResolveWriterRepository(
        INotificationDispatchRepository notificationRepository)
    {
        return notificationRepository as INotificationWriterRepository
            ?? throw new InvalidOperationException("O repositorio de despacho tambem deve implementar escrita de notificacoes.");
    }
}
