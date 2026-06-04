using AuthCore.Application.UseCases.Authentication.GetUserSessions;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.GetUserSessions;

public sealed class GetUserSessionsUseCaseTests
{
    [Fact]
    public async Task Execute_WhenUserHasActiveSessions_ShouldReturnCurrentSessionAndOrderedSessions()
    {
        var userId = Guid.NewGuid();
        var olderSession = Session.Restore(
            "session-older",
            "public-session-older",
            userId,
            SessionStatus.Active,
            "security-stamp-older",
            new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 10, 15, 0, DateTimeKind.Utc),
            "127.0.0.1",
            "Browser A",
            null,
            null);
        var latestSession = Session.Restore(
            "session-latest",
            "public-session-latest",
            userId,
            SessionStatus.Active,
            "security-stamp-latest",
            new DateTime(2026, 4, 18, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 13, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 11, 30, 0, DateTimeKind.Utc),
            "127.0.0.2",
            "Browser B",
            null,
            null);
        var durableSessionRepository = new UnorderedDurableSessionRepository([olderSession, latestSession]);
        var useCase = new GetUserSessionsUseCase(durableSessionRepository);

        var result = await useCase.Execute(new GetUserSessionsQuery
        {
            UserId = userId,
            CurrentSessionId = latestSession.SessionId
        });

        Assert.Equal(latestSession.PublicSessionId, result.CurrentSessionId);
        Assert.Collection(
            result.Sessions,
            session =>
            {
                Assert.Equal(latestSession.PublicSessionId, session.SessionId);
                Assert.Equal("127.0.0.2", session.IpAddress);
            },
            session =>
            {
                Assert.Equal(olderSession.PublicSessionId, session.SessionId);
                Assert.Equal("127.0.0.1", session.IpAddress);
            });
    }

    private sealed class UnorderedDurableSessionRepository : IDurableSessionRepository
    {
        /// <summary>
        /// Campo que armazena sessions.
        /// </summary>
        private readonly IReadOnlyCollection<Session> _sessions;

        public UnorderedDurableSessionRepository(IReadOnlyCollection<Session> sessions)
        {
            _sessions = sessions;
        }

        public Task AddAsync(Session session)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(Session session)
        {
            throw new NotSupportedException();
        }

        public Task<Session?> GetByIdentifierHashAsync(string sessionIdentifierHash, SessionIdentifier identifier)
        {
            throw new NotSupportedException();
        }

        public Task<Session?> GetByPublicSessionIdAsync(string publicSessionId)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId)
        {
            IReadOnlyCollection<Session> sessions = _sessions
                .Where(session => session.UserId == userId)
                .ToArray();

            return Task.FromResult(sessions);
        }

        public Task RevokeActiveByUserIdAsync(Guid userId, SessionRevocationReason reason, DateTime revokedAtUtc)
        {
            throw new NotSupportedException();
        }
    }
}
