using System.Security.Cryptography;
using System.Text;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace AuthCore.Infrastructure.Security.Emails;

/// <summary>
/// Representa serviço de geração de verificação de e-mail com SHA-256.
/// </summary>
internal sealed class Sha256EmailVerificationService : IEmailVerificationService
{
    /// <summary>
    /// Campo que armazena email verification options.
    /// </summary>
    private readonly EmailVerificationOptions _emailVerificationOptions;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="emailVerificationOptions">Configurações da verificação de e-mail.</param>
    public Sha256EmailVerificationService(IOptions<EmailVerificationOptions> emailVerificationOptions)
    {
        ArgumentNullException.ThrowIfNull(emailVerificationOptions);

        _emailVerificationOptions = emailVerificationOptions.Value;
    }


    /// <summary>
    /// Operação para gerar um código OTP com o respectivo hash.
    /// </summary>
    /// <returns>Material gerado da verificação.</returns>
    public EmailVerificationMaterial Create()
    {
        var code = EmailVerification.GenerateCode();

        return new EmailVerificationMaterial
        {
            Code = code,
            Hash = ComputeHash(code)
        };
    }

    /// <summary>
    /// Operação para calcular o hash do código informado.
    /// </summary>
    /// <param name="code">Código OTP em texto puro.</param>
    /// <returns>Hash persistido do código.</returns>
    public string ComputeHash(string code)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code)
            ? string.Empty
            : code.Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode));

        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Operação para obter a data de expiração do código.
    /// </summary>
    /// <returns>Data de expiração em UTC.</returns>
    public DateTime GetExpiresAtUtc()
    {
        return DateTime.UtcNow.AddMinutes(_emailVerificationOptions.ExpiresInMinutes);
    }

    /// <summary>
    /// Operação para obter a data limite de cooldown.
    /// </summary>
    /// <returns>Data do cooldown em UTC.</returns>
    public DateTime GetCooldownUntilUtc()
    {
        return DateTime.UtcNow.AddSeconds(_emailVerificationOptions.CooldownSeconds);
    }

    /// <summary>
    /// Operação para obter o máximo de tentativas permitidas.
    /// </summary>
    /// <returns>Quantidade máxima de tentativas.</returns>
    public int GetMaxAttempts()
    {
        return _emailVerificationOptions.MaxAttempts;
    }
}
