using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa limitador em memória para tentativas de login.
/// </summary>
internal sealed class InMemoryLoginRateLimiter : ILoginRateLimiter
{
    /// <summary>
    /// Campo que armazena entries.
    /// </summary>
    private readonly Dictionary<string, RateLimitEntry> _entries = [];
    /// <summary>
    /// Campo que armazena sync.
    /// </summary>
    private readonly object _sync = new();
    /// <summary>
    /// Campo que armazena time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;
    /// <summary>
    /// Campo que armazena login rate limit options.
    /// </summary>
    private readonly LoginRateLimitOptions _loginRateLimitOptions;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="timeProvider">Provedor de data e hora do processo.</param>
    /// <param name="loginRateLimitOptions">Configurações da limitação de login.</param>
    public InMemoryLoginRateLimiter(
        TimeProvider timeProvider,
        IOptions<LoginRateLimitOptions> loginRateLimitOptions)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(loginRateLimitOptions);

        _timeProvider = timeProvider;
        _loginRateLimitOptions = loginRateLimitOptions.Value;
    }


    /// <summary>
    /// Operação para registrar uma tentativa de login.
    /// </summary>
    /// <param name="ipAddress">Endereço IP da tentativa.</param>
    /// <param name="email">E-mail informado no login.</param>
    /// <returns>Resultado da avaliação do limite atual.</returns>
    public Task<LoginRateLimitResult> TryAcquireAsync(string? ipAddress, string? email)
    {
        var now = _timeProvider.GetUtcNow();
        var window = TimeSpan.FromMinutes(_loginRateLimitOptions.WindowMinutes);
        var normalizedIp = NormalizeIpAddress(ipAddress);
        var normalizedEmail = NormalizeEmail(email);

        lock (_sync)
        {
            var ipEntry = GetEntry($"ip:{normalizedIp}", now, window);
            var emailEntry = GetEntry($"email:{normalizedEmail}", now, window);

            var ipRetryAfter = GetRetryAfter(ipEntry, now, window, _loginRateLimitOptions.MaxAttemptsPerIp);
            var emailRetryAfter = GetRetryAfter(emailEntry, now, window, _loginRateLimitOptions.MaxAttemptsPerEmail);

            if (ipRetryAfter > TimeSpan.Zero || emailRetryAfter > TimeSpan.Zero)
            {
                return Task.FromResult(LoginRateLimitResult.Block(
                    ipRetryAfter > emailRetryAfter
                        ? ipRetryAfter
                        : emailRetryAfter));
            }

            ipEntry.Attempts++;
            emailEntry.Attempts++;

            return Task.FromResult(LoginRateLimitResult.Allow());
        }
    }


    /// <summary>
    /// Operação para obter ou reiniciar a entrada da janela corrente.
    /// </summary>
    /// <param name="key">Chave da partição do rate limit.</param>
    /// <param name="now">Data e hora atual.</param>
    /// <param name="window">Janela configurada.</param>
    /// <returns>Entrada válida para a janela atual.</returns>
    private RateLimitEntry GetEntry(string key, DateTimeOffset now, TimeSpan window)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new RateLimitEntry(now);
            _entries[key] = entry;
            return entry;
        }

        if (now - entry.WindowStartedAtUtc >= window)
        {
            entry.WindowStartedAtUtc = now;
            entry.Attempts = 0;
        }

        return entry;
    }

    /// <summary>
    /// Operação para calcular quanto falta para a janela atual expirar.
    /// </summary>
    /// <param name="entry">Entrada da partição avaliada.</param>
    /// <param name="now">Data e hora atual.</param>
    /// <param name="window">Janela configurada.</param>
    /// <param name="maxAttempts">Quantidade máxima permitida na janela.</param>
    /// <returns>Tempo restante para tentar novamente.</returns>
    private static TimeSpan GetRetryAfter(
        RateLimitEntry entry,
        DateTimeOffset now,
        TimeSpan window,
        int maxAttempts)
    {
        if (entry.Attempts < maxAttempts)
            return TimeSpan.Zero;

        var retryAfter = window - (now - entry.WindowStartedAtUtc);
        return retryAfter > TimeSpan.Zero
            ? retryAfter
            : TimeSpan.Zero;
    }

    /// <summary>
    /// Operação para normalizar o endereço IP.
    /// </summary>
    /// <param name="ipAddress">Endereço IP informado.</param>
    /// <returns>Valor normalizado para particionamento.</returns>
    private static string NormalizeIpAddress(string? ipAddress)
    {
        return string.IsNullOrWhiteSpace(ipAddress)
            ? "unknown"
            : ipAddress.Trim();
    }

    /// <summary>
    /// Operação para normalizar o e-mail informado.
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    /// <returns>Valor normalizado para particionamento.</returns>
    private static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Representa o estado de uma partição do rate limit.
    /// </summary>
    private sealed class RateLimitEntry
    {
        /// <summary>
        /// Data de início da janela atual.
        /// </summary>
        public DateTimeOffset WindowStartedAtUtc { get; set; }

        /// <summary>
        /// Quantidade de tentativas já registradas na janela.
        /// </summary>
        public int Attempts { get; set; }

        /// <summary>
        /// Operação para criar instância da classe.
        /// </summary>
        /// <param name="windowStartedAtUtc">Início da janela atual.</param>
        public RateLimitEntry(DateTimeOffset windowStartedAtUtc)
        {
            WindowStartedAtUtc = windowStartedAtUtc;
            Attempts = 0;
        }
    }

}
