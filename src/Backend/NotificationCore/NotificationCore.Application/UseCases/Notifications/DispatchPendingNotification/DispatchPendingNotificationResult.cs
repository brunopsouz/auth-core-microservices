namespace NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;

/// <summary>
/// Representa resultado do despacho de notificações pendentes.
/// </summary>
public sealed class DispatchPendingNotificationResult
{
    /// <summary>
    /// Quantidade de notificações encontradas para processamento.
    /// </summary>
    public int Found { get; init; }

    /// <summary>
    /// Quantidade de notificações enviadas.
    /// </summary>
    public int Sent { get; init; }

    /// <summary>
    /// Quantidade de notificações agendadas para retry.
    /// </summary>
    public int RetryScheduled { get; init; }

    /// <summary>
    /// Quantidade de notificações finalizadas sem entrega.
    /// </summary>
    public int DeadLettered { get; init; }

    /// <summary>
    /// Quantidade de retries agendados para verificação de e-mail.
    /// </summary>
    public int EmailVerificationRetries { get; init; }

    /// <summary>
    /// Quantidade de retries agendados para e-mail de teste.
    /// </summary>
    public int TestEmailRetries { get; init; }

    /// <summary>
    /// Quantidade de retries agendados para demais transacionais.
    /// </summary>
    public int OtherTransactionalRetries { get; init; }
}
