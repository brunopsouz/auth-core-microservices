using FluentMigrator;

namespace NotificationCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.INITIAL_NOTIFICATION_TEMPLATES, "Seed initial notification templates")]
/// <summary>
/// Representa a migração de seed dos templates transacionais iniciais.
/// </summary>
internal sealed class Version0000002 : VersionBase
{
    private const int EMAIL_CHANNEL = 1;
    private const string AUTH_EMAIL_CONFIRMATION_TEMPLATE_KEY = "auth.email-confirmation";
    private const string AUTH_EMAIL_CONFIRMATION_SUBJECT = "Confirme seu e-mail";
    private const string AUTH_EMAIL_CONFIRMATION_TEXT_BODY = "Seu código de confirmação é {{confirmationCode}}. Ele expira em {{expiresInMinutes}} minutos.";
    private const string AUTH_EMAIL_CONFIRMATION_HTML_BODY = """
        <p>Seu código de confirmação é <strong>{{confirmationCode}}</strong>.</p>
        <p>Ele expira em {{expiresInMinutes}} minutos.</p>
        """;

    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        CreateTemplateUniqueIndex();
        SeedAuthEmailConfirmationTemplate();
    }


    /// <summary>
    /// Operação para criar índice idempotente de templates por chave, canal e versão.
    /// </summary>
    private void CreateTemplateUniqueIndex()
    {
        Execute.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationTemplates_TemplateKey_Channel_Version"
            ON "NotificationTemplates" ("TemplateKey", "Channel", "Version");
            """);
    }

    /// <summary>
    /// Operação para criar template inicial de confirmação de e-mail.
    /// </summary>
    private void SeedAuthEmailConfirmationTemplate()
    {
        Execute.Sql($"""
            INSERT INTO "NotificationTemplates"
            (
                "Id",
                "TemplateKey",
                "Channel",
                "Subject",
                "HtmlBody",
                "TextBody",
                "Version",
                "IsActive",
                "CreatedAtUtc",
                "UpdatedAtUtc"
            )
            VALUES
            (
                '11111111-1111-1111-1111-111111111111',
                '{AUTH_EMAIL_CONFIRMATION_TEMPLATE_KEY}',
                {EMAIL_CHANNEL},
                '{AUTH_EMAIL_CONFIRMATION_SUBJECT}',
                '{AUTH_EMAIL_CONFIRMATION_HTML_BODY}',
                '{AUTH_EMAIL_CONFIRMATION_TEXT_BODY}',
                1,
                TRUE,
                TIMESTAMPTZ '2026-05-08 00:00:00+00',
                TIMESTAMPTZ '2026-05-08 00:00:00+00'
            )
            ON CONFLICT ("TemplateKey", "Channel", "Version") DO UPDATE
            SET
                "Subject" = EXCLUDED."Subject",
                "HtmlBody" = EXCLUDED."HtmlBody",
                "TextBody" = EXCLUDED."TextBody",
                "IsActive" = EXCLUDED."IsActive",
                "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc";
            """);
    }

}
