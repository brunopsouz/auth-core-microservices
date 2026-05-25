using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de conexão com o Redis.
/// </summary>
internal sealed class RedisOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// String de conexão do Redis.
    /// </summary>
    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Prefixo aplicado às chaves armazenadas.
    /// </summary>
    public string KeyPrefix { get; init; } = "authcore";
}
