using AuthCore.Domain.Common.Exceptions;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa validador de origem confiavel de requisicoes por cookie.
/// </summary>
internal sealed class TrustedOriginValidator : ITrustedOriginValidator
{
    private readonly CsrfOptions _csrfOptions;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="csrfOptions">Configuracoes de origens permitidas.</param>
    public TrustedOriginValidator(IOptions<CsrfOptions> csrfOptions)
    {
        ArgumentNullException.ThrowIfNull(csrfOptions);

        _csrfOptions = csrfOptions.Value;
    }

    /// <inheritdoc />
    public void Validate(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var origin = request.Headers.Origin.ToString();
        var referer = request.Headers.Referer.ToString();
        var source = !string.IsNullOrWhiteSpace(origin)
            ? origin
            : referer;

        if (string.IsNullOrWhiteSpace(source))
            throw new ForbiddenException("A origem da requisicao nao foi informada.");

        if (!TryNormalizeOrigin(source, out var normalizedSource))
            throw new ForbiddenException("A origem da requisicao e invalida.");

        if (IsAllowedOrigin(request, normalizedSource))
            return;

        throw new ForbiddenException("A origem da requisicao nao e permitida.");
    }

    private bool IsAllowedOrigin(HttpRequest request, string requestOrigin)
    {
        var allowedOrigins = _csrfOptions.AllowedOrigins
            .Select(NormalizeOrigin)
            .Where(origin => !string.IsNullOrWhiteSpace(origin));

        if (allowedOrigins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase))
            return true;

        if (!request.Host.HasValue)
            return false;

        var currentRequestOrigin = $"{request.Scheme}://{request.Host.Value}";
        return string.Equals(currentRequestOrigin, requestOrigin, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeOrigin(string origin)
    {
        return TryNormalizeOrigin(origin, out var normalizedOrigin)
            ? normalizedOrigin
            : string.Empty;
    }

    private static bool TryNormalizeOrigin(string origin, out string normalizedOrigin)
    {
        normalizedOrigin = string.Empty;

        if (string.IsNullOrWhiteSpace(origin))
            return false;

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
            return false;

        normalizedOrigin = uri.GetLeftPart(UriPartial.Authority);
        return !string.IsNullOrWhiteSpace(normalizedOrigin);
    }
}
