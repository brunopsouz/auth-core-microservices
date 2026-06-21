using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AuthCore.IntegrationTests.Passports;

/// <summary>
/// Verifica a persistencia PostgreSQL de sessoes duraveis.
/// </summary>
public sealed class DurableSessionPersistenceIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    /// <summary>
    /// Campo que armazena a fixture compartilhada.
    /// </summary>
    private readonly PostgreSqlIntegrationFixture _fixture;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="fixture">Fixture compartilhada de banco PostgreSQL.</param>
    public DurableSessionPersistenceIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }


    /// <summary>
    /// Verifica se uma sessao criada pode ser persistida e lida pelo hash do identificador opaco.
    /// </summary>
    [Fact]
    public async Task Persistence_WhenSessionIsCreated_ShouldPersistAndReadByIdentifierHash()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var durableSessionRepository = scope.ServiceProvider.GetRequiredService<IDurableSessionRepository>();
        var sessionIdentifierHasher = scope.ServiceProvider.GetRequiredService<ISessionIdentifierHasher>();
        var user = CreateVerifiedUser($"durable-session.{Guid.NewGuid():N}@authcore.dev");
        var session = Session.Issue(
            user.Id,
            user.SecurityStamp,
            new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc),
            "127.0.0.1",
            "IntegrationTests/1.0");

        await userRepository.AddAsync(user);
        await durableSessionRepository.AddAsync(session);

        var sessionIdentifierHash = sessionIdentifierHasher.ComputeHash(session.Identifier);
        var persistedSession = await durableSessionRepository.GetByIdentifierHashAsync(sessionIdentifierHash, session.Identifier);

        Assert.NotNull(persistedSession);
        Assert.Equal(session.SessionId, persistedSession!.SessionId);
        Assert.Equal(session.PublicSessionId, persistedSession.PublicSessionId);
        Assert.Equal(session.UserId, persistedSession.UserId);
        Assert.Equal(session.Version, persistedSession.Version);
        Assert.Equal(session.SecurityStamp, persistedSession.SecurityStamp);
        Assert.Equal(session.CreatedAtUtc, persistedSession.CreatedAtUtc);
        Assert.Equal(session.ExpiresAtUtc, persistedSession.ExpiresAtUtc);
    }

    /// <summary>
    /// Verifica se uma sessao revogada persiste status, datas e motivo.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenSessionIsRevoked_ShouldPersistRevocationState()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var durableSessionRepository = scope.ServiceProvider.GetRequiredService<IDurableSessionRepository>();
        var user = CreateVerifiedUser($"durable-session.revoke.{Guid.NewGuid():N}@authcore.dev");
        var session = Session.Issue(
            user.Id,
            user.SecurityStamp,
            new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            "127.0.0.2",
            "IntegrationTests/2.0");
        var revokedAtUtc = new DateTime(2026, 6, 20, 12, 30, 0, DateTimeKind.Utc);
        var revokedSession = session.Revoke(SessionRevocationReason.UserRevokedDevice, revokedAtUtc);

        await userRepository.AddAsync(user);
        await durableSessionRepository.AddAsync(session);
        await durableSessionRepository.UpdateAsync(revokedSession);

        var persistedSession = await durableSessionRepository.GetByPublicSessionIdAsync(session.PublicSessionId);

        Assert.NotNull(persistedSession);
        Assert.Equal(SessionStatus.Revoked, persistedSession!.Status);
        Assert.Equal(revokedAtUtc, persistedSession.RevokedAtUtc);
        Assert.Equal(SessionRevocationReason.UserRevokedDevice, persistedSession.RevocationReason);
        Assert.Equal(revokedSession.Version, persistedSession.Version);
        Assert.Equal(session.ExpiresAtUtc, persistedSession.ExpiresAtUtc);
    }

    /// <summary>
    /// Verifica se a atualizacao condicional nao sobrescreve uma sessao revogada.
    /// </summary>
    [Fact]
    public async Task TryUpdateActiveAsync_WhenSessionWasRevoked_ShouldNotUpdateSession()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var durableSessionRepository = scope.ServiceProvider.GetRequiredService<IDurableSessionRepository>();
        var user = CreateVerifiedUser($"durable-session.conditional.{Guid.NewGuid():N}@authcore.dev");
        var nowUtc = DateTime.UtcNow;
        var session = Session.Issue(
            user.Id,
            user.SecurityStamp,
            nowUtc.AddHours(2),
            "127.0.0.2",
            "IntegrationTests/Conditional");

        await userRepository.AddAsync(user);
        await durableSessionRepository.AddAsync(session);
        await durableSessionRepository.UpdateAsync(
            session.Revoke(SessionRevocationReason.UserLogout, nowUtc));

        var staleActiveSession = session.Touch(nowUtc.AddMinutes(1), nowUtc.AddHours(3));
        var wasUpdated = await durableSessionRepository.TryUpdateActiveAsync(
            staleActiveSession,
            nowUtc.AddMinutes(1));
        var persistedSession = await durableSessionRepository.GetByPublicSessionIdAsync(session.PublicSessionId);

        Assert.Null(wasUpdated);
        Assert.NotNull(persistedSession);
        Assert.Equal(SessionStatus.Revoked, persistedSession!.Status);
        Assert.Equal(SessionRevocationReason.UserLogout, persistedSession.RevocationReason);
        Assert.NotEqual(staleActiveSession.ExpiresAtUtc, persistedSession.ExpiresAtUtc);
    }

    /// <summary>
    /// Verifica se a atualizacao condicional rejeita sessao expirada.
    /// </summary>
    [Fact]
    public async Task TryUpdateActiveAsync_WhenSessionIsExpired_ShouldNotUpdateSession()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var durableSessionRepository = scope.ServiceProvider.GetRequiredService<IDurableSessionRepository>();
        var user = CreateVerifiedUser($"durable-session.expired.{Guid.NewGuid():N}@authcore.dev");
        var nowUtc = DateTime.UtcNow;
        var expiredSession = Session.Restore(
            "expired-conditional-session",
            $"sess_{Guid.NewGuid():N}",
            user.Id,
            SessionStatus.Active,
            user.SecurityStamp.Value,
            nowUtc.AddHours(-2),
            nowUtc.AddMinutes(-1),
            nowUtc.AddHours(-1),
            "127.0.0.3",
            "IntegrationTests/Expired",
            null,
            null);

        await userRepository.AddAsync(user);
        await durableSessionRepository.AddAsync(expiredSession);

        var staleActiveSession = expiredSession.Touch(nowUtc, nowUtc.AddHours(2));
        var wasUpdated = await durableSessionRepository.TryUpdateActiveAsync(staleActiveSession, nowUtc);
        var persistedSession = await durableSessionRepository.GetByPublicSessionIdAsync(expiredSession.PublicSessionId);

        Assert.Null(wasUpdated);
        Assert.NotNull(persistedSession);
        Assert.Equal(expiredSession.ExpiresAtUtc, persistedSession!.ExpiresAtUtc);
    }

    /// <summary>
    /// Verifica se a atualizacao condicional rejeita carimbo de seguranca divergente.
    /// </summary>
    [Fact]
    public async Task TryUpdateActiveAsync_WhenSecurityStampDiverges_ShouldNotUpdateSession()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var durableSessionRepository = scope.ServiceProvider.GetRequiredService<IDurableSessionRepository>();
        var user = CreateVerifiedUser($"durable-session.stamp.{Guid.NewGuid():N}@authcore.dev");
        var nowUtc = DateTime.UtcNow;
        var persistedSession = Session.Issue(
            user.Id,
            user.SecurityStamp,
            nowUtc.AddHours(2),
            "127.0.0.4",
            "IntegrationTests/Stamp");
        var divergentSession = Session.Restore(
            persistedSession.SessionId,
            persistedSession.PublicSessionId,
            persistedSession.UserId,
            persistedSession.Status,
            SecurityStamp.Create().Value,
            persistedSession.CreatedAtUtc,
            nowUtc.AddHours(3),
            nowUtc,
            persistedSession.IpAddress,
            persistedSession.UserAgent,
            null,
            null);

        await userRepository.AddAsync(user);
        await durableSessionRepository.AddAsync(persistedSession);

        var wasUpdated = await durableSessionRepository.TryUpdateActiveAsync(divergentSession, nowUtc);
        var reloadedSession = await durableSessionRepository.GetByPublicSessionIdAsync(
            persistedSession.PublicSessionId);

        Assert.Null(wasUpdated);
        Assert.NotNull(reloadedSession);
        Assert.Equal(persistedSession.SecurityStamp, reloadedSession!.SecurityStamp);
        Assert.Equal(persistedSession.ExpiresAtUtc, reloadedSession.ExpiresAtUtc);
    }

    [Fact]
    public async Task TryRevokeAsync_WhenRefreshAdvancedVersion_ShouldRevokeWithCurrentDatabaseVersion()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var durableSessionRepository = scope.ServiceProvider.GetRequiredService<IDurableSessionRepository>();
        var user = CreateVerifiedUser($"durable-session.concurrent-revoke.{Guid.NewGuid():N}@authcore.dev");
        var nowUtc = DateTime.UtcNow;
        var session = Session.Issue(
            user.Id,
            user.SecurityStamp,
            nowUtc.AddHours(2),
            "127.0.0.10",
            "IntegrationTests/ConcurrentRevoke");

        await userRepository.AddAsync(user);
        await durableSessionRepository.AddAsync(session);

        var refreshedSession = await durableSessionRepository.TryUpdateActiveAsync(
            session.Touch(nowUtc.AddMinutes(1), nowUtc.AddHours(3)),
            nowUtc);
        var revokedSession = await durableSessionRepository.TryRevokeAsync(
            session.Revoke(SessionRevocationReason.UserLogout, nowUtc.AddMinutes(2)));

        Assert.NotNull(refreshedSession);
        Assert.NotNull(revokedSession);
        Assert.Equal(refreshedSession!.Version + 1, revokedSession!.Version);
        Assert.Equal(SessionStatus.Revoked, revokedSession.Status);
    }

    /// <summary>
    /// Verifica se a listagem por usuario retorna apenas as sessoes do usuario consultado.
    /// </summary>
    [Fact]
    public async Task ListByUserIdAsync_WhenUserHasSessions_ShouldReturnOnlyOwnedSessions()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var durableSessionRepository = scope.ServiceProvider.GetRequiredService<IDurableSessionRepository>();
        var user = CreateVerifiedUser($"durable-session.list.{Guid.NewGuid():N}@authcore.dev");
        var anotherUser = CreateVerifiedUser($"durable-session.other.{Guid.NewGuid():N}@authcore.dev");
        var nowUtc = DateTime.UtcNow;
        var firstSession = Session.Issue(
            user.Id,
            user.SecurityStamp,
            nowUtc.AddHours(2),
            "127.0.0.3",
            "Browser A");
        var secondSession = Session.Issue(
            user.Id,
            user.SecurityStamp,
            nowUtc.AddHours(3),
            "127.0.0.4",
            "Browser B");
        var foreignSession = Session.Issue(
            anotherUser.Id,
            anotherUser.SecurityStamp,
            nowUtc.AddHours(4),
            "127.0.0.5",
            "Browser C");
        var revokedSession = Session.Issue(
            user.Id,
            user.SecurityStamp,
            nowUtc.AddHours(5),
            "127.0.0.6",
            "Browser D")
            .Revoke(SessionRevocationReason.UserLogout, nowUtc);
        var expiredSession = Session.Restore(
            "expired-session",
            "sess_expired_list",
            user.Id,
            SessionStatus.Active,
            user.SecurityStamp.Value,
            nowUtc.AddHours(-2),
            nowUtc.AddHours(-1),
            nowUtc.AddHours(-2),
            "127.0.0.7",
            "Browser E",
            null,
            null);

        await userRepository.AddAsync(user);
        await userRepository.AddAsync(anotherUser);
        await durableSessionRepository.AddAsync(firstSession);
        await durableSessionRepository.AddAsync(secondSession);
        await durableSessionRepository.AddAsync(foreignSession);
        await durableSessionRepository.AddAsync(revokedSession);
        await durableSessionRepository.AddAsync(expiredSession);

        var persistedSessions = await durableSessionRepository.ListByUserIdAsync(user.Id);

        Assert.Equal(2, persistedSessions.Count);
        Assert.All(persistedSessions, session => Assert.Equal(user.Id, session.UserId));
        Assert.Contains(persistedSessions, session => session.PublicSessionId == firstSession.PublicSessionId);
        Assert.Contains(persistedSessions, session => session.PublicSessionId == secondSession.PublicSessionId);
        Assert.DoesNotContain(persistedSessions, session => session.PublicSessionId == foreignSession.PublicSessionId);
        Assert.DoesNotContain(persistedSessions, session => session.PublicSessionId == revokedSession.PublicSessionId);
        Assert.DoesNotContain(persistedSessions, session => session.PublicSessionId == expiredSession.PublicSessionId);
    }

    /// <summary>
    /// Verifica se a revogacao em massa afeta apenas sessoes ativas do usuario alvo.
    /// </summary>
    [Fact]
    public async Task RevokeActiveByUserIdAsync_WhenUserHasMixedSessions_ShouldRevokeOnlyActiveOwnedSessions()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var durableSessionRepository = scope.ServiceProvider.GetRequiredService<IDurableSessionRepository>();
        var user = CreateVerifiedUser($"durable-session.bulk.{Guid.NewGuid():N}@authcore.dev");
        var anotherUser = CreateVerifiedUser($"durable-session.bulk.other.{Guid.NewGuid():N}@authcore.dev");
        var nowUtc = new DateTime(2026, 6, 25, 18, 0, 0, DateTimeKind.Utc);
        var activeSession = Session.Issue(
            user.Id,
            user.SecurityStamp,
            nowUtc.AddDays(7),
            "127.0.0.6",
            "Browser D");
        var expiredSession = Session.Restore(
            "session-expirada",
            "sess_expired",
            user.Id,
            SessionStatus.Expired,
            user.SecurityStamp.Value,
            nowUtc.AddDays(-10),
            nowUtc.AddMinutes(-1),
            nowUtc.AddDays(-1),
            "127.0.0.7",
            "Browser E",
            null,
            null);
        var revokedSession = Session.Restore(
            "session-revogada",
            "sess_revoked",
            user.Id,
            SessionStatus.Revoked,
            user.SecurityStamp.Value,
            nowUtc.AddDays(-9),
            nowUtc.AddDays(5),
            nowUtc.AddDays(-2),
            "127.0.0.8",
            "Browser F",
            nowUtc.AddDays(-1),
            SessionRevocationReason.UserLogout);
        var foreignSession = Session.Issue(
            anotherUser.Id,
            anotherUser.SecurityStamp,
            nowUtc.AddDays(7),
            "127.0.0.9",
            "Browser G");

        await userRepository.AddAsync(user);
        await userRepository.AddAsync(anotherUser);
        await durableSessionRepository.AddAsync(activeSession);
        await durableSessionRepository.AddAsync(expiredSession);
        await durableSessionRepository.AddAsync(revokedSession);
        await durableSessionRepository.AddAsync(foreignSession);

        var revokedSessions = await durableSessionRepository.RevokeActiveByUserIdAsync(
            user.Id,
            SessionRevocationReason.PasswordChanged,
            nowUtc);

        var persistedActiveSession = await durableSessionRepository.GetByPublicSessionIdAsync(activeSession.PublicSessionId);
        var persistedExpiredSession = await durableSessionRepository.GetByPublicSessionIdAsync(expiredSession.PublicSessionId);
        var persistedRevokedSession = await durableSessionRepository.GetByPublicSessionIdAsync(revokedSession.PublicSessionId);
        var persistedForeignSession = await durableSessionRepository.GetByPublicSessionIdAsync(foreignSession.PublicSessionId);

        Assert.NotNull(persistedActiveSession);
        Assert.Equal(SessionStatus.Revoked, persistedActiveSession!.Status);
        Assert.Equal(nowUtc, persistedActiveSession.RevokedAtUtc);
        Assert.Equal(SessionRevocationReason.PasswordChanged, persistedActiveSession.RevocationReason);
        Assert.Equal(activeSession.Version + 1, persistedActiveSession.Version);
        Assert.Contains(revokedSessions, session => session.PublicSessionId == activeSession.PublicSessionId);
        Assert.NotNull(persistedExpiredSession);
        Assert.Equal(SessionStatus.Expired, persistedExpiredSession!.Status);
        Assert.Null(persistedExpiredSession.RevokedAtUtc);
        Assert.NotNull(persistedRevokedSession);
        Assert.Equal(SessionStatus.Revoked, persistedRevokedSession!.Status);
        Assert.Equal(SessionRevocationReason.UserLogout, persistedRevokedSession.RevocationReason);
        Assert.NotNull(persistedForeignSession);
        Assert.Equal(SessionStatus.Active, persistedForeignSession!.Status);
        Assert.Null(persistedForeignSession.RevokedAtUtc);
    }


    /// <summary>
    /// Operacao para criar um usuario verificado para os testes.
    /// </summary>
    /// <param name="email">E-mail do usuario.</param>
    /// <returns>Usuario pronto para persistencia.</returns>
    private static User CreateVerifiedUser(string email)
    {
        var user = User.Register(
            firstName: "Auth",
            lastName: "Core",
            email: email,
            contact: "11999999999",
            role: Role.User);

        user.VerifyEmail(new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc));

        return user;
    }
}
