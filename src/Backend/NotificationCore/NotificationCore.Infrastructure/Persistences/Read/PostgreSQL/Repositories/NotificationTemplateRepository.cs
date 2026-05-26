using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Infrastructure.Abstractions.Data;
using NotificationCore.Infrastructure.Notifications.Templates;
using Npgsql;

namespace NotificationCore.Infrastructure.Persistences.Read.PostgreSQL.Repositories;

/// <summary>
/// Representa repositório PostgreSQL de leitura de templates de notificação.
/// </summary>
internal sealed class NotificationTemplateRepository : INotificationTemplateRepository
{
    /// <summary>
    /// Campo que armazena database session.
    /// </summary>
    private readonly IDatabaseSession _databaseSession;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="databaseSession">Sessão atual de banco de dados.</param>
    public NotificationTemplateRepository(IDatabaseSession databaseSession)
    {
        _databaseSession = databaseSession;
    }


    /// <summary>
    /// Operação para listar templates ativos.
    /// </summary>
    /// <returns>Lista de templates ativos.</returns>
    public async Task<IReadOnlyCollection<NotificationTemplate>> ListActiveAsync()
    {
        const string sql = """
            SELECT
                "TemplateKey",
                "Channel",
                "Subject",
                "HtmlBody",
                "TextBody"
            FROM "NotificationTemplates"
            WHERE "IsActive" = TRUE
            ORDER BY "TemplateKey", "Channel", "Version" DESC;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);
        await using var reader = await command.ExecuteReaderAsync();

        var templates = new List<NotificationTemplate>();

        while (await reader.ReadAsync())
            templates.Add(ReadTemplate(reader));

        return templates;
    }

    /// <summary>
    /// Operação para obter o template ativo mais recente.
    /// </summary>
    /// <param name="templateKey">Chave do template.</param>
    /// <param name="channel">Canal da notificação.</param>
    /// <returns>Template ativo mais recente ou nulo.</returns>
    public async Task<NotificationTemplate?> GetActiveAsync(string templateKey, NotificationChannel channel)
    {
        const string sql = """
            SELECT
                "TemplateKey",
                "Channel",
                "Subject",
                "HtmlBody",
                "TextBody"
            FROM "NotificationTemplates"
            WHERE "TemplateKey" = @TemplateKey
              AND "Channel" = @Channel
              AND "IsActive" = TRUE
            ORDER BY "Version" DESC
            LIMIT 1;
            """;

        var connection = await _databaseSession.GetOpenConnectionAsync();
        await using var command = CreateCommand(connection, sql);

        command.Parameters.AddWithValue("TemplateKey", templateKey.Trim());
        command.Parameters.AddWithValue("Channel", (int)channel);

        return await ReadTemplateAsync(command);
    }


    /// <summary>
    /// Operação para criar comando SQL respeitando a transação atual.
    /// </summary>
    /// <param name="connection">Conexão aberta da sessão.</param>
    /// <param name="sql">Comando SQL a ser executado.</param>
    /// <returns>Comando pronto para uso.</returns>
    private NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql)
    {
        return new NpgsqlCommand(sql, connection, _databaseSession.CurrentTransaction);
    }

    /// <summary>
    /// Operação para materializar template a partir do comando SQL.
    /// </summary>
    /// <param name="command">Comando SQL configurado.</param>
    /// <returns>Template materializado ou nulo.</returns>
    private static async Task<NotificationTemplate?> ReadTemplateAsync(NpgsqlCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return ReadTemplate(reader);
    }

    /// <summary>
    /// Operação para materializar template a partir do leitor SQL.
    /// </summary>
    /// <param name="reader">Leitor SQL posicionado no registro.</param>
    /// <returns>Template materializado.</returns>
    private static NotificationTemplate ReadTemplate(NpgsqlDataReader reader)
    {
        return new NotificationTemplate
        {
            TemplateKey = reader.GetString(reader.GetOrdinal("TemplateKey")),
            Channel = (NotificationChannel)reader.GetInt32(reader.GetOrdinal("Channel")),
            Subject = reader.GetString(reader.GetOrdinal("Subject")),
            HtmlBody = reader.GetString(reader.GetOrdinal("HtmlBody")),
            TextBody = reader.GetString(reader.GetOrdinal("TextBody"))
        };
    }

}
