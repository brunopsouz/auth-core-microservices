using System.Text.Json;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa store de ticket temporario de onboarding Google em cookie protegido.
/// </summary>
internal sealed class GoogleOnboardingTicketStore : IGoogleOnboardingTicketStore
{
    private const string CookieName = "auth.google-onboarding";
    private const string ProtectorPurpose = "AuthCore.GoogleOnboardingTicket.v1";
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(10);

    private readonly AuthCookieOptions _authCookieOptions;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    public GoogleOnboardingTicketStore(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<AuthCookieOptions> authCookieOptions,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(authCookieOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _authCookieOptions = authCookieOptions.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public void Append(HttpResponse response, CompleteGoogleOnboardingTicketCommand command)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(command);

        var expiresAtUtc = _timeProvider.GetUtcNow().UtcDateTime.Add(TicketLifetime);
        var ticket = new GoogleOnboardingTicket(
            command.ProviderUserId,
            command.Email,
            command.EmailVerified,
            command.FullName ?? string.Empty,
            command.PictureUrl ?? string.Empty,
            expiresAtUtc);
        var protectedPayload = _protector.Protect(JsonSerializer.Serialize(ticket));

        response.Cookies.Append(
            CookieName,
            protectedPayload,
            CreateTicketCookieOptions(expiresAtUtc));
    }

    /// <inheritdoc />
    public GoogleOnboardingTicket? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Cookies.TryGetValue(CookieName, out var protectedPayload)
            || string.IsNullOrWhiteSpace(protectedPayload))
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(protectedPayload);
            var ticket = JsonSerializer.Deserialize<GoogleOnboardingTicket>(payload);

            if (ticket is null || ticket.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime)
                return null;

            return ticket;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(
            CookieName,
            CreateExpiredTicketCookieOptions());
    }

    private CookieOptions CreateTicketCookieOptions(DateTime expiresAtUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = _authCookieOptions.Secure,
            SameSite = ResolveSameSite(_authCookieOptions.SameSite),
            Path = "/",
            Expires = new DateTimeOffset(expiresAtUtc)
        };
    }

    private CookieOptions CreateExpiredTicketCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = _authCookieOptions.Secure,
            SameSite = ResolveSameSite(_authCookieOptions.SameSite),
            Path = "/"
        };
    }

    private static SameSiteMode ResolveSameSite(string sameSite)
    {
        return Enum.TryParse<SameSiteMode>(sameSite?.Trim(), ignoreCase: true, out var resolvedSameSite)
            ? resolvedSameSite
            : SameSiteMode.Lax;
    }
}
