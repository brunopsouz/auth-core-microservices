using System.Data;
using FluentMigrator;

namespace AuthCore.Infrastructure.Persistences.Migrations.Versions;

[Migration(DatabaseVersions.TABLE_AUTH_SESSIONS, "Create table to store durable auth sessions")]
/// <summary>
/// Representa a migracao de criacao da tabela de sessoes duraveis.
/// </summary>
public sealed class Version0000010 : ForwardOnlyMigration
{
    /// <summary>
    /// Operacao para aplicar a migracao da versao atual.
    /// </summary>
    public override void Up()
    {
        Create.Table("auth_sessions")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("public_session_id").AsString(64).NotNullable()
            .WithColumn("user_id").AsGuid().NotNullable()
            .WithColumn("session_identifier_hash").AsString(128).NotNullable()
            .WithColumn("status").AsInt16().NotNullable()
            .WithColumn("security_stamp").AsString(128).NotNullable()
            .WithColumn("device_name").AsString(512).Nullable()
            .WithColumn("user_agent").AsString(2048).Nullable()
            .WithColumn("ip_address").AsString(128).Nullable()
            .WithColumn("created_at_utc").AsDateTime().NotNullable()
            .WithColumn("expires_at_utc").AsDateTime().NotNullable()
            .WithColumn("last_seen_at_utc").AsDateTime().Nullable()
            .WithColumn("revoked_at_utc").AsDateTime().Nullable()
            .WithColumn("revocation_reason").AsInt16().Nullable();

        Create.Index("UX_auth_sessions_public_session_id")
            .OnTable("auth_sessions")
            .OnColumn("public_session_id").Ascending()
            .WithOptions().Unique();

        Create.Index("UX_auth_sessions_session_identifier_hash")
            .OnTable("auth_sessions")
            .OnColumn("session_identifier_hash").Ascending()
            .WithOptions().Unique();

        Create.Index("IX_auth_sessions_user_id_status")
            .OnTable("auth_sessions")
            .OnColumn("user_id").Ascending()
            .OnColumn("status").Ascending();

        Create.Index("IX_auth_sessions_expires_at_utc")
            .OnTable("auth_sessions")
            .OnColumn("expires_at_utc").Ascending();

        Create.Index("IX_auth_sessions_status_expires_at_utc")
            .OnTable("auth_sessions")
            .OnColumn("status").Ascending()
            .OnColumn("expires_at_utc").Ascending();

        Create.Index("IX_auth_sessions_user_id_created_at_utc")
            .OnTable("auth_sessions")
            .OnColumn("user_id").Ascending()
            .OnColumn("created_at_utc").Ascending();

        Create.ForeignKey("FK_auth_sessions_Users_user_id")
            .FromTable("auth_sessions").ForeignColumn("user_id")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDeleteOrUpdate(Rule.Cascade);
    }
}
