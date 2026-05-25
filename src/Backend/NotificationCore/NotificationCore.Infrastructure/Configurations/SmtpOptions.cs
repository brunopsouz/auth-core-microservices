using System.ComponentModel.DataAnnotations;

namespace NotificationCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de envio SMTP.
/// </summary>
internal sealed class SmtpOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "Smtp";

    /// <summary>
    /// Endereço do servidor SMTP.
    /// </summary>
    [Required]
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// Porta de conexão do SMTP.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    /// <summary>
    /// Nome do usuário de acesso SMTP.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Senha do usuário de acesso SMTP.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Indica se a conexão deve usar TLS.
    /// </summary>
    public bool UseTls { get; init; } = true;

    /// <summary>
    /// E-mail remetente usado nas notificações.
    /// </summary>
    [Required]
    [EmailAddress]
    public string SenderEmail { get; init; } = string.Empty;

    /// <summary>
    /// Nome remetente usado nas notificações.
    /// </summary>
    [Required]
    public string SenderName { get; init; } = "NotificationCore";

    /// <summary>
    /// Tempo limite da operação SMTP em segundos.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;
}
