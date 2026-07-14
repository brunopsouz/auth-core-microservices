namespace NotificationCore.Application.UseCases.Notifications.DispatchPendingNotification;

/// <summary>
/// Representa contadores mutaveis do despacho.
/// </summary>
internal sealed class DispatchCounters
{
    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="found">Quantidade de notificacoes encontradas.</param>
    public DispatchCounters(int found)
    {
        Found = found;
    }

    /// <summary>
    /// Quantidade de notificacoes encontradas.
    /// </summary>
    public int Found { get; set; }

    /// <summary>
    /// Quantidade de notificacoes enviadas.
    /// </summary>
    public int Sent { get; set; }

    /// <summary>
    /// Quantidade de notificacoes com retry agendado.
    /// </summary>
    public int RetryScheduled { get; set; }

    /// <summary>
    /// Quantidade de notificacoes finalizadas sem entrega.
    /// </summary>
    public int DeadLettered { get; set; }

    /// <summary>
    /// Quantidade de retries agendados para verificação de e-mail.
    /// </summary>
    public int EmailVerificationRetries { get; private set; }

    /// <summary>
    /// Quantidade de retries agendados para e-mail de teste.
    /// </summary>
    public int TestEmailRetries { get; private set; }

    /// <summary>
    /// Quantidade de retries agendados para demais transacionais.
    /// </summary>
    public int OtherTransactionalRetries { get; private set; }

    /// <summary>
    /// Operação para incrementar retry agendado por tipo controlado.
    /// </summary>
    /// <param name="templateKey">Chave do template da notificação.</param>
    public void IncrementRetryScheduled(string templateKey)
    {
        if (string.Equals(templateKey, "auth.email-confirmation", StringComparison.Ordinal))
        {
            EmailVerificationRetries++;
            return;
        }

        if (string.Equals(templateKey, "notificationcore.test-email", StringComparison.Ordinal))
        {
            TestEmailRetries++;
            return;
        }

        OtherTransactionalRetries++;
    }

    /// <summary>
    /// Operacao para criar resultado do despacho.
    /// </summary>
    /// <returns>Resultado do despacho.</returns>
    public DispatchPendingNotificationResult ToResult()
    {
        return new DispatchPendingNotificationResult
        {
            Found = Found,
            Sent = Sent,
            RetryScheduled = RetryScheduled,
            DeadLettered = DeadLettered,
            EmailVerificationRetries = EmailVerificationRetries,
            TestEmailRetries = TestEmailRetries,
            OtherTransactionalRetries = OtherTransactionalRetries
        };
    }
}
