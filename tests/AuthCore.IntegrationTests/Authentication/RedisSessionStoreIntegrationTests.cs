using AuthCore.Domain.Passports;
using AuthCore.Infrastructure.Configurations;
using AuthCore.Infrastructure.Services.Caching;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuthCore.IntegrationTests.Authentication;

/// <summary>
/// Verifica o protocolo atomico de sessoes no Redis.
/// </summary>
public sealed class RedisSessionStoreIntegrationTests : IClassFixture<RedisSessionStoreFixture>
{
    private readonly RedisSessionStoreFixture _fixture;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="fixture">Fixture compartilhada do Redis.</param>
    public RedisSessionStoreIntegrationTests(RedisSessionStoreFixture fixture)
    {
        _fixture = fixture;
    }


    [Fact]
    public async Task RevokeAsync_WhenSaveRunsConcurrently_ShouldKeepSessionRevoked()
    {
        if (!_fixture.IsAvailable)
            return;

        var prefix = $"authcore-tests:{Guid.NewGuid():N}";
        var store = _fixture.CreateStore(prefix);
        var session = Session.Issue(
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(10),
            "127.0.0.1",
            "RedisIntegrationTests/ConcurrentRevoke");
        var revokedSession = session.Revoke(
            SessionRevocationReason.UserLogout,
            session.CreatedAtUtc.AddSeconds(1));

        try
        {
            await Task.WhenAll(
                store.TrySaveAsync(session),
                store.RevokeAsync(revokedSession));

            Assert.Null(await store.GetByIdAsync(session.SessionId));
            Assert.False(await store.TrySaveAsync(session.AdvanceVersion()));
        }
        finally
        {
            await _fixture.DeleteSessionKeysAsync(prefix, session);
        }
    }

    [Fact]
    public async Task TrySaveAsync_WhenCachedVersionIsNewer_ShouldRejectOlderProjection()
    {
        if (!_fixture.IsAvailable)
            return;

        var prefix = $"authcore-tests:{Guid.NewGuid():N}";
        var store = _fixture.CreateStore(prefix);
        var session = Session.Issue(
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(10),
            "127.0.0.1",
            "RedisIntegrationTests/Version");
        var newerSession = session.AdvanceVersion();

        try
        {
            Assert.True(await store.TrySaveAsync(newerSession));
            Assert.False(await store.TrySaveAsync(session));
            Assert.True(await store.TrySaveAsync(newerSession));

            var cachedSession = await store.GetByIdAsync(session.SessionId);

            Assert.NotNull(cachedSession);
            Assert.Equal(newerSession.Version, cachedSession!.Version);
        }
        finally
        {
            await _fixture.DeleteSessionKeysAsync(prefix, session);
        }
    }
}

/// <summary>
/// Representa fixture opcional para testes com Redis real.
/// </summary>
public sealed class RedisSessionStoreFixture : IAsyncLifetime
{
    private IConnectionMultiplexer? _connectionMultiplexer;

    public bool IsAvailable => _connectionMultiplexer?.IsConnected == true;


    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("AUTHCORE_TEST_REDIS")
            ?? "localhost:6379,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

        try
        {
            _connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(connectionString);
        }
        catch (RedisException)
        {
            _connectionMultiplexer = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (_connectionMultiplexer is null)
            return;

        await _connectionMultiplexer.CloseAsync();
        _connectionMultiplexer.Dispose();
    }

    internal RedisSessionStore CreateStore(string prefix)
    {
        if (_connectionMultiplexer is null)
            throw new InvalidOperationException("O Redis de teste nao esta disponivel.");

        return new RedisSessionStore(
            _connectionMultiplexer,
            Options.Create(new RedisOptions
            {
                ConnectionString = _connectionMultiplexer.Configuration,
                KeyPrefix = prefix
            }));
    }

    public async Task DeleteSessionKeysAsync(string prefix, Session session)
    {
        if (_connectionMultiplexer is null)
            return;

        var database = _connectionMultiplexer.GetDatabase();
        await database.KeyDeleteAsync(
        [
            $"{prefix}:session:{session.SessionId}",
            $"{prefix}:revoked-session:{session.PublicSessionId}",
            $"{prefix}:user:sessions:{session.UserId}"
        ]);
    }
}
