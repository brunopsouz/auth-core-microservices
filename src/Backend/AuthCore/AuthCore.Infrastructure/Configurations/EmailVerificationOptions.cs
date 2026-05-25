using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações da verificação de e-mail.
/// </summary>
internal sealed class EmailVerificationOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Auth:EmailVerification";

    /// <summary>
    /// Tempo de vida do código OTP em minutos.
    /// </summary>
    [Range(1, 1440)]
    public int ExpiresInMinutes { get; init; } = 15;

    /// <summary>
    /// Tempo de cooldown para reenvio em segundos.
    /// </summary>
    [Range(0, 86400)]
    public int CooldownSeconds { get; init; } = 60;

    /// <summary>
    /// Quantidade máxima de tentativas inválidas.
    /// </summary>
    [Range(1, 20)]
    public int MaxAttempts { get; init; } = 5;
}
