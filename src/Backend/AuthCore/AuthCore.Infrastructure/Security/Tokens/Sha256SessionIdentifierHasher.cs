using System.Security.Cryptography;
using System.Text;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;

namespace AuthCore.Infrastructure.Security.Tokens;

/// <summary>
/// Representa hasher SHA-256 do identificador opaco de sessao.
/// </summary>
internal sealed class Sha256SessionIdentifierHasher : ISessionIdentifierHasher
{
    /// <summary>
    /// Operacao para calcular hash do identificador opaco de sessao.
    /// </summary>
    /// <param name="identifier">Identificador opaco de sessao.</param>
    /// <returns>Hash do identificador opaco.</returns>
    public string ComputeHash(SessionIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        var bytes = Encoding.UTF8.GetBytes(identifier.Value);
        var hashBytes = SHA256.HashData(bytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
