using AuthCore.Domain.Passports;

namespace AuthCore.Domain.Passports.Repositories;

/// <summary>
/// Define operações para geração e validação de verificação de e-mail.
/// </summary>
public interface IEmailVerificationService
{
    /// <summary>
    /// Operação para gerar um código OTP com o respectivo hash.
    /// </summary>
    /// <returns>Material gerado da verificação.</returns>
    EmailVerificationMaterial Create();

    /// <summary>
    /// Operação para calcular o hash do código informado.
    /// </summary>
    /// <param name="code">Código OTP em texto puro.</param>
    /// <returns>Hash persistido do código.</returns>
    string ComputeHash(string code);

    /// <summary>
    /// Operação para obter a data de expiração do código.
    /// </summary>
    /// <returns>Data de expiração em UTC.</returns>
    DateTime GetExpiresAtUtc();

    /// <summary>
    /// Operação para obter a data limite de cooldown para reenvio.
    /// </summary>
    /// <returns>Data do cooldown em UTC.</returns>
    DateTime GetCooldownUntilUtc();

    /// <summary>
    /// Operação para obter o máximo de tentativas permitidas.
    /// </summary>
    /// <returns>Quantidade máxima de tentativas.</returns>
    int GetMaxAttempts();
}
