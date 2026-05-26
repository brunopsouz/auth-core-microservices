using AuthCore.Application.Authentication.UseCases.GetUserSessions;
using AuthCore.Domain.Passports.Aggregates;
using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Application.UnitTests.Authentication.UseCases.GetUserSessions;

public sealed class GetUserSessionsUseCaseTests
{
    [Fact]
    public async Task Execute_WhenUserHasActiveSessions_ShouldReturnCurrentSessionAndOrderedSessions()
    {
        var userId = Guid.NewGuid();
        var olderSession = Session.Restore(
            "session-older",
            userId,
            new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 10, 15, 0, DateTimeKind.Utc),
            "127.0.0.1",
            "Browser A",
            null);
        var latestSession = Session.Restore(
            "session-latest",
            userId,
            new DateTime(2026, 4, 18, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 13, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 18, 11, 30, 0, DateTimeKind.Utc),
            "127.0.0.2",
            "Browser B",
            null);
        var sessionStore = new UnorderedSessionStore([olderSession, latestSession]);
        var useCase = new GetUserSessionsUseCase(sessionStore);

        var result = await useCase.Execute(new GetUserSessionsQuery
        {
            UserId = userId,
            CurrentSessionId = latestSession.SessionId
        });

        Assert.Equal(latestSession.SessionId, result.CurrentSessionId);
        Assert.Collection(
            result.Sessions,
            session =>
            {
                Assert.Equal(latestSession.SessionId, session.SessionId);
                Assert.Equal("127.0.0.2", session.IpAddress);
            },
            session =>
            {
                Assert.Equal(olderSession.SessionId, session.SessionId);
                Assert.Equal("127.0.0.1", session.IpAddress);
            });
    }

    private sealed class UnorderedSessionStore : ISessionStore
    {
        /// <summary>
        /// Campo que armazena sessions.
        /// </summary>
        private readonly IReadOnlyCollection<Session> _sessions;

        public UnorderedSessionStore(IReadOnlyCollection<Session> sessions)
        {
            _sessions = sessions;
        }

        public Task SaveAsync(Session session)
        {
            throw new NotSupportedException();
        }

        public Task<Session?> GetByIdAsync(string sessionId)
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

        public Task RevokeAsync(string sessionId)
        {
            throw new NotSupportedException();
        }

        public Task RevokeAllAsync(Guid userId)
        {
            throw new NotSupportedException();
        }
    }
}
