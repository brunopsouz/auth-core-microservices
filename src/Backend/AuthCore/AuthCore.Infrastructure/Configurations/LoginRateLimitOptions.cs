using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de limitação do login.
/// </summary>
internal sealed class LoginRateLimitOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Auth:LoginRateLimit";

    /// <summary>
    /// Quantidade máxima de tentativas por endereço IP dentro da janela.
    /// </summary>
    [Range(1, 1000)]
    public int MaxAttemptsPerIp { get; init; } = 20;

    /// <summary>
    /// Quantidade máxima de tentativas por e-mail dentro da janela.
    /// </summary>
    [Range(1, 1000)]
    public int MaxAttemptsPerEmail { get; init; } = 5;

    /// <summary>
    /// Duração, em minutos, da janela de limitação.
    /// </summary>
    [Range(1, 1440)]
    public int WindowMinutes { get; init; } = 5;
}
