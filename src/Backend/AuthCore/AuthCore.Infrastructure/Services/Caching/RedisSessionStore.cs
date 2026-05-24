using System.Text.Json;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuthCore.Infrastructure.Services.Caching;

/// <summary>
/// Representa store Redis de sessões autenticadas.
/// </summary>
internal sealed class RedisSessionStore : ISessionStore
{
    private readonly IDatabase _database;
    private readonly RedisOptions _redisOptions;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="connectionMultiplexer">Conexão compartilhada com o Redis.</param>
    /// <param name="redisOptions">Configurações do Redis.</param>
    public RedisSessionStore(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisOptions> redisOptions)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        ArgumentNullException.ThrowIfNull(redisOptions);

        _database = connectionMultiplexer.GetDatabase();
        _redisOptions = redisOptions.Value;
    }

    #endregion

    /// <summary>
    /// Operação para persistir uma sessão autenticada.
    /// </summary>
    /// <param name="session">Sessão autenticada a ser persistida.</param>
    public async Task SaveAsync(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var sessionKey = GetSessionKey(session.SessionId);
        var userSessionsKey = GetUserSessionsKey(session.UserId);
        var payload = JsonSerializer.Serialize(new SessionCacheModel
        {
            SessionId = session.SessionId,
            UserId = session.UserId,
            CreatedAtUtc = session.CreatedAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            LastSeenAtUtc = session.LastSeenAtUtc,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            RevokedAtUtc = session.RevokedAtUtc
        }, _serializerOptions);
        var ttl = session.ExpiresAtUtc - DateTime.UtcNow;

        if (ttl <= TimeSpan.Zero)
            ttl = TimeSpan.FromSeconds(1);

        await _database.StringSetAsync(sessionKey, payload, ttl);
        await _database.SetAddAsync(userSessionsKey, session.SessionId);
    }

    /// <summary>
    /// Operação para obter uma sessão pelo identificador.
    /// </summary>
    /// <param name="sessionId">Identificador público da sessão.</param>
    /// <returns>Sessão encontrada ou nula.</returns>
    public async Task<Session?> GetByIdAsync(string sessionId)
    {
        var sessionValue = await _database.StringGetAsync(GetSessionKey(sessionId));

        if (!sessionValue.HasValue)
            return null;

        var sessionModel = JsonSerializer.Deserialize<SessionCacheModel>(sessionValue!, _serializerOptions);

        if (sessionModel is null)
            return null;

        return Session.Restore(
            sessionModel.SessionId,
            sessionModel.UserId,
            sessionModel.CreatedAtUtc,
            sessionModel.ExpiresAtUtc,
            sessionModel.LastSeenAtUtc,
            sessionModel.IpAddress,
            sessionModel.UserAgent,
            sessionModel.RevokedAtUtc);
    }

    /// <summary>
    /// Operação para listar as sessões ativas de um usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Sessões ativas encontradas.</returns>
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
    /// Operação para revogar uma sessão específica.
    /// </summary>
    /// <param name="sessionId">Identificador público da sessão.</param>
    public async Task RevokeAsync(string sessionId)
    {
        var session = await GetByIdAsync(sessionId);

        if (session is null)
            return;

        await _database.KeyDeleteAsync(GetSessionKey(sessionId));
        await _database.SetRemoveAsync(GetUserSessionsKey(session.UserId), session.SessionId);
    }

    /// <summary>
    /// Operação para revogar todas as sessões de um usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    public async Task RevokeAllAsync(Guid userId)
    {
        var userSessionsKey = GetUserSessionsKey(userId);
        var sessionIds = await _database.SetMembersAsync(userSessionsKey);

        foreach (var sessionId in sessionIds)
            await _database.KeyDeleteAsync(GetSessionKey(sessionId.ToString()));

        await _database.KeyDeleteAsync(userSessionsKey);
    }

    #region Helpers

    /// <summary>
    /// Operação para obter a chave Redis da sessão.
    /// </summary>
    /// <param name="sessionId">Identificador público da sessão.</param>
    /// <returns>Chave Redis da sessão.</returns>
    private string GetSessionKey(string sessionId)
    {
        return $"{_redisOptions.KeyPrefix}:session:{sessionId.Trim()}";
    }

    /// <summary>
    /// Operação para obter a chave Redis do índice de sessões do usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <returns>Chave Redis do índice do usuário.</returns>
    private string GetUserSessionsKey(Guid userId)
    {
        return $"{_redisOptions.KeyPrefix}:user:sessions:{userId}";
    }

    /// <summary>
    /// Representa o payload serializado da sessão em cache.
    /// </summary>
    private sealed class SessionCacheModel
    {
        public string SessionId { get; set; } = string.Empty;

        public Guid UserId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? LastSeenAtUtc { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime? RevokedAtUtc { get; set; }
    }

    #endregion
}
