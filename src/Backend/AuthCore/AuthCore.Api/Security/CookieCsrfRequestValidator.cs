using AuthCore.Domain.Common.Exceptions;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa validador de origem para mutações autenticadas por cookie.
/// </summary>
internal sealed class CookieCsrfRequestValidator : ICsrfRequestValidator
{
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<CookieCsrfRequestValidator> _logger;
    /// <summary>
    /// Campo que armazena csrf options.
    /// </summary>
    private readonly CsrfOptions _csrfOptions;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="logger">Logger da validação CSRF.</param>
    /// <param name="csrfOptions">Configurações de proteção CSRF.</param>
    public CookieCsrfRequestValidator(
        ILogger<CookieCsrfRequestValidator> logger,
        IOptions<CsrfOptions> csrfOptions)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(csrfOptions);

        _logger = logger;
        _csrfOptions = csrfOptions.Value;
    }


    /// <summary>
    /// Operação para validar a origem da requisição HTTP atual.
    /// </summary>
    /// <param name="request">Requisição autenticada por cookie.</param>
    public void Validate(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (TryResolveRequestOrigin(request, out var requestOrigin)
            && IsAllowedOrigin(request, requestOrigin))
        {
            return;
        }

        _logger.LogWarning(
            "Requisição bloqueada por proteção CSRF. Method={Method} Path={Path} Origin={Origin} Referer={Referer}",
            request.Method,
            request.Path,
            request.Headers.Origin.ToString(),
            request.Headers.Referer.ToString());

        throw new ForbiddenException("A origem da requisição não é permitida.");
    }


    /// <summary>
    /// Operação para indicar se a origem da requisição está permitida.
    /// </summary>
    /// <param name="request">Requisição HTTP atual.</param>
    /// <param name="requestOrigin">Origem normalizada da requisição.</param>
    /// <returns><c>true</c> quando a origem está autorizada; caso contrário, <c>false</c>.</returns>
    private bool IsAllowedOrigin(HttpRequest request, string requestOrigin)
    {
        var allowedOrigins = _csrfOptions.AllowedOrigins
            .Select(NormalizeOrigin)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .ToArray();

        if (allowedOrigins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase))
            return true;

        if (!request.Host.HasValue)
            return false;

        var currentRequestOrigin = $"{request.Scheme}://{request.Host.Value}";
        return string.Equals(currentRequestOrigin, requestOrigin, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Operação para obter a origem declarada pela requisição.
    /// </summary>
    /// <param name="request">Requisição HTTP atual.</param>
    /// <param name="origin">Origem normalizada quando encontrada.</param>
    /// <returns><c>true</c> quando a origem foi encontrada; caso contrário, <c>false</c>.</returns>
    private static bool TryResolveRequestOrigin(HttpRequest request, out string origin)
    {
        origin = string.Empty;

        var originHeader = request.Headers.Origin.ToString();

        if (TryNormalizeOrigin(originHeader, out origin))
            return true;

        var refererHeader = request.Headers.Referer.ToString();
        return TryNormalizeOrigin(refererHeader, out origin);
    }

    /// <summary>
    /// Operação para normalizar uma origem configurada.
    /// </summary>
    /// <param name="origin">Origem configurada.</param>
    /// <returns>Origem normalizada.</returns>
    private static string NormalizeOrigin(string origin)
    {
        return TryNormalizeOrigin(origin, out var normalizedOrigin)
            ? normalizedOrigin
            : string.Empty;
    }

    /// <summary>
    /// Operação para normalizar uma origem informada em header.
    /// </summary>
    /// <param name="origin">Origem informada.</param>
    /// <param name="normalizedOrigin">Origem normalizada.</param>
    /// <returns><c>true</c> quando a origem é válida; caso contrário, <c>false</c>.</returns>
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
