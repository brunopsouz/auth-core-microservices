using FluentMigrator;

namespace NotificationCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.INITIAL_NOTIFICATION_SCHEMA, "Create initial notification schema")]
/// <summary>
/// Representa a migração de criação do schema inicial de notificações.
/// </summary>
public sealed class Version0000001 : VersionBase
{
    /// <summary>
    /// Operação para aplicar a migração da versão atual.
    /// </summary>
    public override void Up()
    {
        CreateInboxMessagesTable();
        CreateNotificationsTable();
        CreateNotificationDeliveryAttemptsTable();
        CreateNotificationTemplatesTable();
        CreateIndexes();
    }


    /// <summary>
    /// Operação para criar a tabela de inbox.
    /// </summary>
    private void CreateInboxMessagesTable()
    {
        Create.Table("InboxMessages")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("MessageId").AsGuid().NotNullable()
            .WithColumn("Source").AsString(100).NotNullable()
            .WithColumn("MessageType").AsString(200).NotNullable()
            .WithColumn("ConsumerName").AsString(200).NotNullable()
            .WithColumn("Payload").AsString(int.MaxValue).NotNullable()
            .WithColumn("ReceivedAtUtc").AsCustom("timestamp with time zone").NotNullable()
            .WithColumn("ProcessedAtUtc").AsCustom("timestamp with time zone").Nullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("Error").AsString(2000).Nullable()
            .WithColumn("RetryCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAtUtc").AsCustom("timestamp with time zone").NotNullable()
            .WithColumn("UpdatedAtUtc").AsCustom("timestamp with time zone").NotNullable();
    }

    /// <summary>
    /// Operação para criar a tabela de notificações.
    /// </summary>
    private void CreateNotificationsTable()
    {
        CreateTableWithId("Notifications")
            .WithColumn("Source").AsString(100).NotNullable()
            .WithColumn("CorrelationId").AsString(200).NotNullable()
            .WithColumn("IdempotencyKey").AsString(300).NotNullable()
            .WithColumn("Channel").AsInt32().NotNullable()
            .WithColumn("Recipient").AsString(320).NotNullable()
            .WithColumn("TemplateKey").AsString(200).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("Priority").AsInt32().NotNullable()
            .WithColumn("RequestedAtUtc").AsCustom("timestamp with time zone").NotNullable()
            .WithColumn("ScheduledAtUtc").AsCustom("timestamp with time zone").NotNullable()
            .WithColumn("CreatedAtUtc").AsCustom("timestamp with time zone").NotNullable()
            .WithColumn("SentAtUtc").AsCustom("timestamp with time zone").Nullable()
            .WithColumn("FailedAtUtc").AsCustom("timestamp with time zone").Nullable()
            .WithColumn("LastError").AsString(2000).Nullable();
    }

    /// <summary>
    /// Operação para criar a tabela de tentativas de entrega.
    /// </summary>
    private void CreateNotificationDeliveryAttemptsTable()
    {
        CreateTableWithId("NotificationDeliveryAttempts")
            .WithColumn("NotificationId").AsGuid().NotNullable()
            .WithColumn("Provider").AsString(100).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("AttemptNumber").AsInt32().NotNullable()
            .WithColumn("StartedAtUtc").AsCustom("timestamp with time zone").NotNullable()
            .WithColumn("FinishedAtUtc").AsCustom("timestamp with time zone").NotNullable()
            .WithColumn("ErrorCode").AsString(100).Nullable()
            .WithColumn("ErrorMessage").AsString(2000).Nullable()
            .WithColumn("ProviderMessageId").AsString(300).Nullable();

        Create.ForeignKey("FK_NotificationDeliveryAttempts_Notifications_NotificationId")
            .FromTable("NotificationDeliveryAttempts").ForeignColumn("NotificationId")
            .ToTable("Notifications").PrimaryColumn("Id");
    }

    /// <summary>
    /// Operação para criar a tabela de templates.
    /// </summary>
    private void CreateNotificationTemplatesTable()
    {
        CreateTableWithId("NotificationTemplates")
            .WithColumn("TemplateKey").AsString(200).NotNullable()
            .WithColumn("Channel").AsInt32().NotNullable()
            .WithColumn("Subject").AsString(300).NotNullable()
            .WithColumn("HtmlBody").AsString(int.MaxValue).NotNullable()
            .WithColumn("TextBody").AsString(int.MaxValue).NotNullable()
            .WithColumn("Version").AsInt32().NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAtUtc").AsCustom("timestamp with time zone").NotNullable()
            .WithColumn("UpdatedAtUtc").AsCustom("timestamp with time zone").NotNullable();
    }

    /// <summary>
    /// Operação para criar os índices do schema inicial.
    /// </summary>
    private void CreateIndexes()
    {
        Create.Index("IX_InboxMessages_MessageId_MessageType_ConsumerName")
            .OnTable("InboxMessages")
            .OnColumn("MessageId").Ascending()
            .OnColumn("MessageType").Ascending()
            .OnColumn("ConsumerName").Ascending()
            .WithOptions().Unique();

        Create.Index("IX_InboxMessages_Status_CreatedAtUtc")
            .OnTable("InboxMessages")
            .OnColumn("Status").Ascending()
            .OnColumn("CreatedAtUtc").Ascending();

        Create.Index("IX_InboxMessages_ProcessedAtUtc")
            .OnTable("InboxMessages")
            .OnColumn("ProcessedAtUtc").Ascending();

        Create.Index("IX_Notifications_IdempotencyKey")
            .OnTable("Notifications")
            .OnColumn("IdempotencyKey").Ascending()
            .WithOptions().Unique();

        Create.Index("IX_Notifications_Status")
            .OnTable("Notifications")
            .OnColumn("Status").Ascending();

        Create.Index("IX_Notifications_CorrelationId")
            .OnTable("Notifications")
            .OnColumn("CorrelationId").Ascending();

        Create.Index("IX_Notifications_ScheduledAtUtc")
            .OnTable("Notifications")
            .OnColumn("ScheduledAtUtc").Ascending();

        Create.Index("IX_NotificationDeliveryAttempts_NotificationId_AttemptNumber")
            .OnTable("NotificationDeliveryAttempts")
            .OnColumn("NotificationId").Ascending()
            .OnColumn("AttemptNumber").Ascending()
            .WithOptions().Unique();
    }

}
