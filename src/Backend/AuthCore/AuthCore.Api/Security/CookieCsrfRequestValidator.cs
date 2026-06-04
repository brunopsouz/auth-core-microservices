using AuthCore.Domain.Common.Exceptions;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa validador de origem e token CSRF para mutacoes autenticadas por cookie.
/// </summary>
internal sealed class CookieCsrfRequestValidator : ICsrfRequestValidator
{
    /// <summary>
    /// Campo que armazena auth cookie options.
    /// </summary>
    private readonly AuthCookieOptions _authCookieOptions;
    /// <summary>
    /// Campo que armazena csrf options.
    /// </summary>
    private readonly CsrfOptions _csrfOptions;
    /// <summary>
    /// Campo que armazena csrf token service.
    /// </summary>
    private readonly ICsrfTokenService _csrfTokenService;
    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<CookieCsrfRequestValidator> _logger;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="logger">Logger da validacao CSRF.</param>
    /// <param name="csrfOptions">Configuracoes de protecao CSRF.</param>
    /// <param name="authCookieOptions">Configuracoes do cookie da sessao.</param>
    /// <param name="csrfTokenService">Servico de token CSRF vinculado a sessao.</param>
    public CookieCsrfRequestValidator(
        ILogger<CookieCsrfRequestValidator> logger,
        IOptions<CsrfOptions> csrfOptions,
        IOptions<AuthCookieOptions> authCookieOptions,
        ICsrfTokenService csrfTokenService)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(csrfOptions);
        ArgumentNullException.ThrowIfNull(authCookieOptions);
        ArgumentNullException.ThrowIfNull(csrfTokenService);

        _logger = logger;
        _csrfOptions = csrfOptions.Value;
        _authCookieOptions = authCookieOptions.Value;
        _csrfTokenService = csrfTokenService;
    }


    /// <summary>
    /// Operacao para validar a origem e o token CSRF da requisicao HTTP atual.
    /// </summary>
    /// <param name="request">Requisicao autenticada por cookie.</param>
    public void Validate(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryResolveCsrfPayload(request, out var sessionId, out var cookieToken, out var headerToken))
        {
            LogBlockedRequest(request);
            throw new ForbiddenException("O token CSRF informado e invalido.");
        }

        if (!string.Equals(cookieToken, headerToken, StringComparison.Ordinal))
        {
            LogBlockedRequest(request);
            throw new ForbiddenException("O token CSRF informado e invalido.");
        }

        if (!_csrfTokenService.IsValid(sessionId, cookieToken))
        {
            LogBlockedRequest(request);
            throw new ForbiddenException("O token CSRF informado e invalido.");
        }

        if (TryResolveRequestOrigin(request, out var requestOrigin)
            && IsAllowedOrigin(request, requestOrigin))
        {
            return;
        }

        LogBlockedRequest(request);
        throw new ForbiddenException("A origem da requisicao nao e permitida.");
    }


    /// <summary>
    /// Operacao para indicar se a origem da requisicao esta permitida.
    /// </summary>
    /// <param name="request">Requisicao HTTP atual.</param>
    /// <param name="requestOrigin">Origem normalizada da requisicao.</param>
    /// <returns><c>true</c> quando a origem esta autorizada; caso contrario, <c>false</c>.</returns>
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
    /// Operacao para obter a origem declarada pela requisicao.
    /// </summary>
    /// <param name="request">Requisicao HTTP atual.</param>
    /// <param name="origin">Origem normalizada quando encontrada.</param>
    /// <returns><c>true</c> quando a origem foi encontrada; caso contrario, <c>false</c>.</returns>
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
    /// Operacao para tentar obter a carga minima necessaria para validar o CSRF.
    /// </summary>
    /// <param name="request">Requisicao HTTP atual.</param>
    /// <param name="sessionId">Identificador opaco da sessao autenticada.</param>
    /// <param name="cookieToken">Token CSRF lido do cookie.</param>
    /// <param name="headerToken">Token CSRF lido do header.</param>
    /// <returns><c>true</c> quando todos os dados foram encontrados; caso contrario, <c>false</c>.</returns>
    private bool TryResolveCsrfPayload(
        HttpRequest request,
        out string sessionId,
        out string cookieToken,
        out string headerToken)
    {
        sessionId = string.Empty;
        cookieToken = string.Empty;
        headerToken = string.Empty;

        if (!request.Cookies.TryGetValue(_authCookieOptions.SessionCookieName, out var currentSessionId)
            || string.IsNullOrWhiteSpace(currentSessionId))
        {
            return false;
        }

        if (!request.Cookies.TryGetValue(_csrfOptions.CookieName, out var currentCookieToken)
            || string.IsNullOrWhiteSpace(currentCookieToken))
        {
            return false;
        }

        if (!request.Headers.TryGetValue(_csrfOptions.HeaderName, out var currentHeaderToken)
            || string.IsNullOrWhiteSpace(currentHeaderToken.ToString()))
        {
            return false;
        }

        sessionId = currentSessionId.Trim();
        cookieToken = currentCookieToken.Trim();
        headerToken = currentHeaderToken.ToString().Trim();

        return true;
    }

    /// <summary>
    /// Operacao para registrar a tentativa bloqueada pela protecao CSRF.
    /// </summary>
    /// <param name="request">Requisicao HTTP bloqueada.</param>
    private void LogBlockedRequest(HttpRequest request)
    {
        _logger.LogWarning(
            "Requisicao bloqueada por protecao CSRF. Method={Method} Path={Path} Origin={Origin} Referer={Referer}",
            request.Method,
            request.Path,
            request.Headers.Origin.ToString(),
            request.Headers.Referer.ToString());
    }

    /// <summary>
    /// Operacao para normalizar uma origem configurada.
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
    /// Operacao para normalizar uma origem informada em header.
    /// </summary>
    /// <param name="origin">Origem informada.</param>
    /// <param name="normalizedOrigin">Origem normalizada.</param>
    /// <returns><c>true</c> quando a origem e valida; caso contrario, <c>false</c>.</returns>
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
