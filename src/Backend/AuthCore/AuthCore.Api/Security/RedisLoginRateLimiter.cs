using System.Security.Cryptography;
using System.Text;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa limitador Redis para tentativas de login.
/// </summary>
internal sealed class RedisLoginRateLimiter : ILoginRateLimiter
{
    /// <summary>
    /// Campo que armazena database.
    /// </summary>
    private readonly IDatabase _database;
    /// <summary>
    /// Campo que armazena redis options.
    /// </summary>
    private readonly RedisOptions _redisOptions;
    /// <summary>
    /// Campo que armazena login rate limit options.
    /// </summary>
    private readonly LoginRateLimitOptions _loginRateLimitOptions;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="connectionMultiplexer">Conexão compartilhada com o Redis.</param>
    /// <param name="redisOptions">Configurações do Redis.</param>
    /// <param name="loginRateLimitOptions">Configurações da limitação de login.</param>
    public RedisLoginRateLimiter(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisOptions> redisOptions,
        IOptions<LoginRateLimitOptions> loginRateLimitOptions)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        ArgumentNullException.ThrowIfNull(redisOptions);
        ArgumentNullException.ThrowIfNull(loginRateLimitOptions);

        _database = connectionMultiplexer.GetDatabase();
        _redisOptions = redisOptions.Value;
        _loginRateLimitOptions = loginRateLimitOptions.Value;
    }


    /// <summary>
    /// Operação para registrar uma tentativa de login.
    /// </summary>
    /// <param name="ipAddress">Endereço IP da tentativa.</param>
    /// <param name="email">E-mail informado no login.</param>
    /// <returns>Resultado da avaliação do limite atual.</returns>
    public async Task<LoginRateLimitResult> TryAcquireAsync(string? ipAddress, string? email)
    {
        var window = TimeSpan.FromMinutes(_loginRateLimitOptions.WindowMinutes);
        var windowBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (long)window.TotalSeconds;
        var ipKey = GetIpKey(ipAddress, windowBucket);
        var emailKey = GetEmailKey(email, windowBucket);

        var ipCount = await IncrementWithExpirationAsync(ipKey, window);
        var emailCount = await IncrementWithExpirationAsync(emailKey, window);

        if (ipCount <= _loginRateLimitOptions.MaxAttemptsPerIp
            && emailCount <= _loginRateLimitOptions.MaxAttemptsPerEmail)
        {
            return LoginRateLimitResult.Allow();
        }

        var ipRetryAfter = ipCount > _loginRateLimitOptions.MaxAttemptsPerIp
            ? await GetRetryAfterAsync(ipKey)
            : TimeSpan.Zero;
        var emailRetryAfter = emailCount > _loginRateLimitOptions.MaxAttemptsPerEmail
            ? await GetRetryAfterAsync(emailKey)
            : TimeSpan.Zero;

        return LoginRateLimitResult.Block(ipRetryAfter > emailRetryAfter ? ipRetryAfter : emailRetryAfter);
    }


    /// <summary>
    /// Operação para incrementar uma chave garantindo expiração da janela.
    /// </summary>
    /// <param name="key">Chave da janela atual.</param>
    /// <param name="window">Duração da janela.</param>
    /// <returns>Valor do contador após o incremento.</returns>
    private async Task<long> IncrementWithExpirationAsync(string key, TimeSpan window)
    {
        var count = await _database.StringIncrementAsync(key);

        if (count == 1)
            await _database.KeyExpireAsync(key, window);

        return count;
    }

    /// <summary>
    /// Operação para obter o tempo restante da janela atual.
    /// </summary>
    /// <param name="key">Chave da janela atual.</param>
    /// <returns>Tempo restante para nova tentativa.</returns>
    private async Task<TimeSpan> GetRetryAfterAsync(string key)
    {
        var ttl = await _database.KeyTimeToLiveAsync(key);

        if (ttl is null || ttl <= TimeSpan.Zero)
            return TimeSpan.FromSeconds(1);

        return ttl.Value;
    }

    /// <summary>
    /// Operação para obter a chave Redis da partição por IP.
    /// </summary>
    /// <param name="ipAddress">Endereço IP informado.</param>
    /// <param name="windowBucket">Bucket da janela atual.</param>
    /// <returns>Chave Redis da partição por IP.</returns>
    private string GetIpKey(string? ipAddress, long windowBucket)
    {
        return $"{_redisOptions.KeyPrefix}:login-rate:ip:{NormalizeIpAddress(ipAddress)}:{windowBucket}";
    }

    /// <summary>
    /// Operação para obter a chave Redis da partição por e-mail.
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    /// <param name="windowBucket">Bucket da janela atual.</param>
    /// <returns>Chave Redis da partição por e-mail.</returns>
    private string GetEmailKey(string? email, long windowBucket)
    {
        return $"{_redisOptions.KeyPrefix}:login-rate:email:{HashEmail(email)}:{windowBucket}";
    }

    /// <summary>
    /// Operação para normalizar o endereço IP.
    /// </summary>
    /// <param name="ipAddress">Endereço IP informado.</param>
    /// <returns>Valor normalizado.</returns>
    private static string NormalizeIpAddress(string? ipAddress)
    {
        return string.IsNullOrWhiteSpace(ipAddress)
            ? "unknown"
            : ipAddress.Trim();
    }

    /// <summary>
    /// Operação para gerar o hash do e-mail informado.
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    /// <returns>Hash hexadecimal em minúsculas.</returns>
    private static string HashEmail(string? email)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

}
