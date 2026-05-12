using System.ComponentModel.DataAnnotations;

namespace NotificationCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações do despachante de notificações.
/// </summary>
public sealed class NotificationDispatcherOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "NotificationDispatcher";

    /// <summary>
    /// Indica se o despachante deve ser executado.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Quantidade de notificações processadas por ciclo.
    /// </summary>
    [Range(1, 500)]
    public int BatchSize { get; init; } = 20;

    /// <summary>
    /// Intervalo de polling em segundos.
    /// </summary>
    [Range(1, 300)]
    public int PollingIntervalSeconds { get; init; } = 10;

    /// <summary>
    /// Intervalo para reagendar falhas temporárias em segundos.
    /// </summary>
    [Range(1, 86400)]
    public int RetryDelaySeconds { get; init; } = 300;

    /// <summary>
    /// Tempo máximo para considerar uma notificação presa em processamento.
    /// </summary>
    [Range(1, 3600)]
    public int ProcessingTimeoutSeconds { get; init; } = 900;
}
