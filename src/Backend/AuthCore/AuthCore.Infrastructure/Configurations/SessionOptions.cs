using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de sessão autenticada.
/// </summary>
internal sealed class SessionOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Auth:Session";

    /// <summary>
    /// Tempo de vida da sessão em minutos.
    /// </summary>
    [Range(1, 43200)]
    public int TtlMinutes { get; init; } = 120;

    /// <summary>
    /// Indica se a sessão usa expiração deslizante.
    /// </summary>
    public bool SlidingTtl { get; init; } = true;

    /// <summary>
    /// Intervalo minimo entre atualizacoes de ultimo uso da sessao em segundos.
    /// </summary>
    [Range(1, 86400)]
    public int LastSeenUpdateIntervalSeconds { get; init; } = 60;
}
