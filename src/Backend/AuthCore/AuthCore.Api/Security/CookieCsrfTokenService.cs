using System.Security.Cryptography;
using System.Text;
using AuthCore.Infrastructure.Configurations;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AuthCore.Api.Security;

/// <summary>
/// Representa servico de token CSRF assinado e vinculado a sessao autenticada.
/// </summary>
internal sealed class CookieCsrfTokenService : ICsrfTokenService
{
    private readonly byte[] _signingKeyBytes;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="csrfOptions">Configuracoes da protecao CSRF.</param>
    public CookieCsrfTokenService(IOptions<CsrfOptions> csrfOptions)
    {
        ArgumentNullException.ThrowIfNull(csrfOptions);

        var signingKey = csrfOptions.Value.SigningKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(signingKey))
            throw new InvalidOperationException("A chave de assinatura do CSRF nao foi configurada.");

        _signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
    }


    /// <summary>
    /// Operacao para gerar token CSRF vinculado ao identificador opaco da sessao.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao autenticada.</param>
    /// <returns>Token CSRF assinado.</returns>
    public string Generate(string sessionId)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var signature = CreateSignature(normalizedSessionId, nonce);

        return $"{nonce}.{signature}";
    }

    /// <summary>
    /// Operacao para validar token CSRF vinculado ao identificador opaco da sessao.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao autenticada.</param>
    /// <param name="token">Token CSRF informado.</param>
    /// <returns><c>true</c> quando o token e valido; caso contrario, <c>false</c>.</returns>
    public bool IsValid(string sessionId, string token)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        var normalizedToken = NormalizeToken(token);
        var segments = normalizedToken.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length != 2)
            return false;

        var expectedSignature = CreateSignature(normalizedSessionId, segments[0]);
        var providedSignatureBytes = Encoding.UTF8.GetBytes(segments[1]);
        var expectedSignatureBytes = Encoding.UTF8.GetBytes(expectedSignature);

        return CryptographicOperations.FixedTimeEquals(providedSignatureBytes, expectedSignatureBytes);
    }


    /// <summary>
    /// Operacao para criar a assinatura do token CSRF.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="nonce">Nonce aleatorio do token.</param>
    /// <returns>Assinatura codificada.</returns>
    private string CreateSignature(string sessionId, string nonce)
    {
        using var hmac = new HMACSHA256(_signingKeyBytes);
        var payloadBytes = Encoding.UTF8.GetBytes($"{sessionId}:{nonce}");
        var signatureBytes = hmac.ComputeHash(payloadBytes);

        return WebEncoders.Base64UrlEncode(signatureBytes);
    }

    /// <summary>
    /// Operacao para normalizar o identificador opaco informado.
    /// </summary>
    /// <param name="sessionId">Identificador informado.</param>
    /// <returns>Identificador normalizado.</returns>
    private static string NormalizeSessionId(string sessionId)
    {
        return string.IsNullOrWhiteSpace(sessionId)
            ? string.Empty
            : sessionId.Trim();
    }

    /// <summary>
    /// Operacao para normalizar o token informado.
    /// </summary>
    /// <param name="token">Token informado.</param>
    /// <returns>Token normalizado.</returns>
    private static string NormalizeToken(string token)
    {
        return string.IsNullOrWhiteSpace(token)
            ? string.Empty
            : token.Trim();
    }
}
