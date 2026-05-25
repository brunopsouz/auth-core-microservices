using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações do processamento da outbox.
/// </summary>
internal sealed class OutboxOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Outbox";

    /// <summary>
    /// Indica se o worker da outbox deve ser executado.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Quantidade de mensagens processadas por ciclo.
    /// </summary>
    [Range(1, 500)]
    public int BatchSize { get; init; } = 20;

    /// <summary>
    /// Intervalo de polling em segundos.
    /// </summary>
    [Range(1, 300)]
    public int PollingIntervalSeconds { get; init; } = 10;

    /// <summary>
    /// Quantidade máxima de tentativas por mensagem.
    /// </summary>
    [Range(1, 100)]
    public int MaxAttempts { get; init; } = 5;
}
