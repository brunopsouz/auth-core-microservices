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
            DeadLettered = DeadLettered
        };
    }
}
