using System.ComponentModel.DataAnnotations;

namespace NotificationCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de banco de dados.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "ConnectionStrings";

    /// <summary>
    /// String de conexão do PostgreSQL.
    /// </summary>
    [Required]
    public string PostgreSql { get; init; } = string.Empty;
}
