using AuthCore.Application.Common.Exceptions;

namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa validador de URL de retorno para autenticacao externa.
/// </summary>
internal sealed class ExternalReturnUrlValidator : IExternalReturnUrlValidator
{
    private const string InvalidReturnUrlMessage = "A URL de retorno informada não é permitida.";

    private readonly IExternalAuthenticationOptionsProvider _optionsProvider;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="optionsProvider">Fornecedor das configuracoes de autenticacao externa.</param>
    public ExternalReturnUrlValidator(IExternalAuthenticationOptionsProvider optionsProvider)
    {
        ArgumentNullException.ThrowIfNull(optionsProvider);

        _optionsProvider = optionsProvider;
    }

    /// <summary>
    /// Operacao para validar e normalizar URL de retorno.
    /// </summary>
    /// <param name="returnUrl">URL de retorno informada pelo cliente.</param>
    /// <returns>URL de retorno segura.</returns>
    public string Validate(string? returnUrl)
    {
        var options = _optionsProvider.GetOptions();

        if (string.IsNullOrWhiteSpace(returnUrl))
            return NormalizeDefaultReturnUrl(options);

        var normalizedReturnUrl = returnUrl.Trim();

        if (IsProtocolRelativeUrl(normalizedReturnUrl)
            || !Uri.TryCreate(normalizedReturnUrl, UriKind.Absolute, out var uri)
            || !IsHttpScheme(uri)
            || !IsAllowedOrigin(uri, options.AllowedReturnUrls))
        {
            throw new ValidationException(InvalidReturnUrlMessage);
        }

        return uri.AbsoluteUri;
    }

    private static string NormalizeDefaultReturnUrl(ExternalAuthenticationOptions options)
    {
        var defaultReturnUrl = options.DefaultReturnUrl?.Trim();

        if (string.IsNullOrWhiteSpace(defaultReturnUrl))
            return "/";

        if (IsSafeRelativeUrl(defaultReturnUrl))
        {
            return defaultReturnUrl;
        }

        if (Uri.TryCreate(defaultReturnUrl, UriKind.Absolute, out var uri)
            && IsHttpScheme(uri)
            && IsAllowedOrigin(uri, options.AllowedReturnUrls))
        {
            return uri.AbsoluteUri;
        }

        throw new ValidationException(InvalidReturnUrlMessage);
    }

    private static bool IsAllowedOrigin(Uri returnUrl, IEnumerable<string> allowedReturnUrls)
    {
        return allowedReturnUrls
            .Select(NormalizeAllowedOrigin)
            .OfType<Uri>()
            .Any(allowedOrigin => HasSameOrigin(returnUrl, allowedOrigin));
    }

    private static Uri? NormalizeAllowedOrigin(string? allowedReturnUrl)
    {
        if (string.IsNullOrWhiteSpace(allowedReturnUrl))
            return null;

        var normalizedAllowedReturnUrl = allowedReturnUrl.Trim();

        if (IsProtocolRelativeUrl(normalizedAllowedReturnUrl)
            || !Uri.TryCreate(normalizedAllowedReturnUrl, UriKind.Absolute, out var uri)
            || !IsHttpScheme(uri))
        {
            return null;
        }

        return uri;
    }

    private static bool HasSameOrigin(Uri returnUrl, Uri allowedOrigin)
    {
        return string.Equals(returnUrl.Scheme, allowedOrigin.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(returnUrl.IdnHost, allowedOrigin.IdnHost, StringComparison.OrdinalIgnoreCase)
            && returnUrl.Port == allowedOrigin.Port;
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocolRelativeUrl(string url)
    {
        return url.StartsWith("//", StringComparison.Ordinal)
            || url.StartsWith(@"\\", StringComparison.Ordinal);
    }

    private static bool IsSafeRelativeUrl(string url)
    {
        return url.StartsWith("/", StringComparison.Ordinal)
            && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
    }
}
