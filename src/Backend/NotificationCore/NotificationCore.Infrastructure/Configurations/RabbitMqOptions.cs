using System.ComponentModel.DataAnnotations;

namespace NotificationCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de conexão com o RabbitMQ.
/// </summary>
internal sealed class RabbitMqOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// Indica se o consumer RabbitMQ deve ser executado.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Endereço do servidor RabbitMQ.
    /// </summary>
    [Required]
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// Porta de conexão do RabbitMQ.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 5672;

    /// <summary>
    /// Host virtual usado na conexão.
    /// </summary>
    [Required]
    public string VirtualHost { get; init; } = "/";

    /// <summary>
    /// Nome do usuário de acesso.
    /// </summary>
    [Required]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Senha do usuário de acesso.
    /// </summary>
    [Required]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Exchange que recebe solicitações de notificação.
    /// </summary>
    [Required]
    public string Exchange { get; init; } = "notification.requests";

    /// <summary>
    /// Chave de roteamento para solicitações de e-mail.
    /// </summary>
    [Required]
    public string RoutingKey { get; init; } = "notification.email.requested";

    /// <summary>
    /// Fila principal para solicitações de e-mail.
    /// </summary>
    [Required]
    public string Queue { get; init; } = "notification.email.requests";

    /// <summary>
    /// Fila de mensagens rejeitadas.
    /// </summary>
    [Required]
    public string DeadLetterQueue { get; init; } = "notification.email.requests.dlq";
}
