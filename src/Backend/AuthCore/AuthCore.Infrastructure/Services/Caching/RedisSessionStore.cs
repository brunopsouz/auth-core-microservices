using System.Text.Json;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuthCore.Infrastructure.Services.Caching;

/// <summary>
/// Representa store Redis de sessoes autenticadas.
/// </summary>
internal sealed class RedisSessionStore : ISessionStore
{
    private const string SAVE_SESSION_SCRIPT = """
        if redis.call('EXISTS', KEYS[2]) == 1 then
            return 0
        end

        local current = redis.call('GET', KEYS[1])

        if current then
            local currentSession = cjson.decode(current)

            if tonumber(currentSession.version or 1) > tonumber(ARGV[2]) then
                return 0
            end

            if tonumber(currentSession.version or 1) == tonumber(ARGV[2]) then
                return 1
            end
        end

        redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[3])
        redis.call('SADD', KEYS[3], ARGV[4])
        return 1
        """;

    private const string REVOKE_SESSION_SCRIPT = """
        local requestedVersion = tonumber(ARGV[1])
        local requestedTtl = tonumber(ARGV[2])
        local existingVersion = tonumber(redis.call('GET', KEYS[1]) or '0')
        local existingTtl = redis.call('PTTL', KEYS[1])
        local tombstoneVersion = math.max(existingVersion, requestedVersion)
        local tombstoneTtl = math.max(existingTtl, requestedTtl)

        redis.call('SET', KEYS[1], tostring(tombstoneVersion), 'PX', tombstoneTtl)

        local sessionIds = redis.call('SMEMBERS', KEYS[2])

        for _, sessionId in ipairs(sessionIds) do
            local sessionKey = ARGV[4] .. sessionId
            local payload = redis.call('GET', sessionKey)

            if payload then
                local cachedSession = cjson.decode(payload)

                if cachedSession.publicSessionId == ARGV[3] then
                    redis.call('DEL', sessionKey)
                    redis.call('SREM', KEYS[2], sessionId)
                end
            else
                redis.call('SREM', KEYS[2], sessionId)
            end
        end

        return 1
        """;

    private const string REMOVE_SESSION_BY_VERSION_SCRIPT = """
        local payload = redis.call('GET', KEYS[1])

        if not payload then
            return 0
        end

        local cachedSession = cjson.decode(payload)

        if tonumber(cachedSession.version or 1) > tonumber(ARGV[1]) then
            return 0
        end

        redis.call('DEL', KEYS[1])
        redis.call('SREM', KEYS[2], ARGV[2])
        return 1
        """;

    private readonly IDatabase _database;
    private readonly RedisOptions _redisOptions;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="connectionMultiplexer">Conexao compartilhada com o Redis.</param>
    /// <param name="redisOptions">Configuracoes do Redis.</param>
    public RedisSessionStore(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisOptions> redisOptions)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        ArgumentNullException.ThrowIfNull(redisOptions);

        _database = connectionMultiplexer.GetDatabase();
        _redisOptions = redisOptions.Value;
    }


    /// <summary>
    /// Operacao para persistir uma nova sessao autenticada.
    /// </summary>
    /// <param name="session">Sessao autenticada a ser persistida.</param>
    public async Task SaveAsync(Session session)
    {
        await SaveAsync(session, CancellationToken.None);
    }

    public async Task SaveAsync(Session session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await TrySaveAsync(session).WaitAsync(cancellationToken))
            throw new InvalidOperationException("A sessao nao pode ser persistida porque foi revogada ou possui versao obsoleta.");
    }

    /// <summary>
    /// Operacao para persistir uma sessao quando nao houver revogacao e a versao for mais recente.
    /// </summary>
    /// <param name="session">Sessao autenticada a ser persistida.</param>
    /// <returns>Verdadeiro quando a sessao foi persistida.</returns>
    public async Task<bool> TrySaveAsync(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var ttl = GetTtl(session.ExpiresAtUtc);
        var payload = Serialize(session);
        var result = await _database.ScriptEvaluateAsync(
            SAVE_SESSION_SCRIPT,
            [
                GetSessionKey(session.SessionId),
                GetRevokedSessionKey(session.PublicSessionId),
                GetUserSessionsKey(session.UserId)
            ],
            [
                payload,
                session.Version,
                (long)ttl.TotalMilliseconds,
                session.SessionId
            ]);

        return (long)result == 1;
    }

    /// <summary>
    /// Operacao para obter uma sessao pelo identificador secreto.
    /// </summary>
    /// <param name="sessionId">Identificador secreto da sessao.</param>
    /// <returns>Sessao encontrada ou nula.</returns>
    public async Task<Session?> GetByIdAsync(string sessionId)
    {
        var sessionValue = await _database.StringGetAsync(GetSessionKey(sessionId));

        if (!sessionValue.HasValue)
            return null;

        var sessionModel = Deserialize(sessionValue);

        if (sessionModel is null)
            return null;

        if (await _database.KeyExistsAsync(GetRevokedSessionKey(sessionModel.PublicSessionId)))
        {
            await RemoveAsync(sessionId);
            return null;
        }

        return Restore(sessionModel);
    }

    /// <summary>
    /// Operacao para listar as sessoes ativas de um usuario.
    /// </summary>
    /// <param name="userId">Identificador interno do usuario.</param>
    /// <returns>Sessoes ativas encontradas.</returns>
    public async Task<IReadOnlyCollection<Session>> ListByUserIdAsync(Guid userId)
    {
        var userSessionsKey = GetUserSessionsKey(userId);
        var sessionIds = await _database.SetMembersAsync(userSessionsKey);
        var sessions = new List<Session>(sessionIds.Length);

        foreach (var sessionId in sessionIds)
        {
            var normalizedSessionId = sessionId.ToString();
            var session = await GetByIdAsync(normalizedSessionId);

            if (session is null)
            {
                await _database.SetRemoveAsync(userSessionsKey, normalizedSessionId);
                continue;
            }

            sessions.Add(session);
        }

        return sessions
            .OrderByDescending(session => session.LastSeenAtUtc ?? session.CreatedAtUtc)
            .ToArray();
    }

    /// <summary>
    /// Operacao para registrar a revogacao de uma sessao.
    /// </summary>
    /// <param name="session">Sessao revogada com a versao persistida.</param>
    public async Task RevokeAsync(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status != SessionStatus.Revoked)
            throw new ArgumentException("A sessao informada deve estar revogada.", nameof(session));

        var ttl = GetTtl(session.ExpiresAtUtc);
        await _database.ScriptEvaluateAsync(
            REVOKE_SESSION_SCRIPT,
            [
                GetRevokedSessionKey(session.PublicSessionId),
                GetUserSessionsKey(session.UserId)
            ],
            [
                session.Version,
                (long)ttl.TotalMilliseconds,
                session.PublicSessionId,
                GetSessionKeyPrefix()
            ]);
    }

    /// <summary>
    /// Operacao para remover uma sessao ativa pelo identificador secreto.
    /// </summary>
    /// <param name="sessionId">Identificador secreto da sessao.</param>
    public async Task RemoveAsync(string sessionId)
    {
        var sessionKey = GetSessionKey(sessionId);
        var sessionValue = await _database.StringGetAsync(sessionKey);

        if (sessionValue.HasValue)
        {
            var sessionModel = Deserialize(sessionValue);

            if (sessionModel is not null)
                await _database.SetRemoveAsync(GetUserSessionsKey(sessionModel.UserId), sessionId);
        }

        await _database.KeyDeleteAsync(sessionKey);
    }

    /// <summary>
    /// Operacao para remover uma sessao apenas quando a versao em cache nao for mais recente.
    /// </summary>
    /// <param name="sessionId">Identificador secreto da sessao.</param>
    /// <param name="maximumVersion">Maior versao que pode ser removida.</param>
    public async Task RemoveWhenVersionIsNotNewerAsync(string sessionId, long maximumVersion)
    {
        var sessionValue = await _database.StringGetAsync(GetSessionKey(sessionId));

        if (!sessionValue.HasValue)
            return;

        var sessionModel = Deserialize(sessionValue);

        if (sessionModel is null)
        {
            await _database.KeyDeleteAsync(GetSessionKey(sessionId));
            return;
        }

        await _database.ScriptEvaluateAsync(
            REMOVE_SESSION_BY_VERSION_SCRIPT,
            [
                GetSessionKey(sessionId),
                GetUserSessionsKey(sessionModel.UserId)
            ],
            [
                maximumVersion,
                sessionId
            ]);
    }


    private string Serialize(Session session)
    {
        return JsonSerializer.Serialize(new SessionCacheModel
        {
            SessionId = session.SessionId,
            PublicSessionId = session.PublicSessionId,
            UserId = session.UserId,
            Status = session.Status,
            Version = session.Version,
            SecurityStamp = session.SecurityStamp.Value,
            CreatedAtUtc = session.CreatedAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            LastSeenAtUtc = session.LastSeenAtUtc,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            RevokedAtUtc = session.RevokedAtUtc,
            RevocationReason = session.RevocationReason
        }, _serializerOptions);
    }

    private SessionCacheModel? Deserialize(RedisValue sessionValue)
    {
        return JsonSerializer.Deserialize<SessionCacheModel>(sessionValue.ToString(), _serializerOptions);
    }

    private static Session Restore(SessionCacheModel sessionModel)
    {
        return Session.Restore(
            sessionModel.SessionId,
            string.IsNullOrWhiteSpace(sessionModel.PublicSessionId)
                ? sessionModel.SessionId
                : sessionModel.PublicSessionId,
            sessionModel.UserId,
            sessionModel.Status == default ? SessionStatus.Active : sessionModel.Status,
            string.IsNullOrWhiteSpace(sessionModel.SecurityStamp)
                ? SecurityStamp.Create().Value
                : sessionModel.SecurityStamp,
            sessionModel.CreatedAtUtc,
            sessionModel.ExpiresAtUtc,
            sessionModel.LastSeenAtUtc,
            sessionModel.IpAddress,
            sessionModel.UserAgent,
            sessionModel.RevokedAtUtc,
            sessionModel.RevocationReason,
            sessionModel.Version <= 0 ? 1 : sessionModel.Version);
    }

    private static TimeSpan GetTtl(DateTime expiresAtUtc)
    {
        var ttl = expiresAtUtc - DateTime.UtcNow;
        return ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(1);
    }

    private string GetSessionKey(string sessionId)
    {
        return $"{GetSessionKeyPrefix()}{sessionId.Trim()}";
    }

    private string GetSessionKeyPrefix()
    {
        return $"{_redisOptions.KeyPrefix}:session:";
    }

    private string GetRevokedSessionKey(string publicSessionId)
    {
        return $"{_redisOptions.KeyPrefix}:revoked-session:{publicSessionId.Trim()}";
    }

    private string GetUserSessionsKey(Guid userId)
    {
        return $"{_redisOptions.KeyPrefix}:user:sessions:{userId}";
    }

    private sealed class SessionCacheModel
    {
        public string SessionId { get; set; } = string.Empty;

        public string PublicSessionId { get; set; } = string.Empty;

        public Guid UserId { get; set; }

        public SessionStatus Status { get; set; }

        public long Version { get; set; }

        public string SecurityStamp { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? LastSeenAtUtc { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime? RevokedAtUtc { get; set; }

        public SessionRevocationReason? RevocationReason { get; set; }
    }
}
