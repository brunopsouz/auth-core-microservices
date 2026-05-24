namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa o material gerado para verificação de e-mail.
/// </summary>
public sealed class EmailVerificationMaterial
{
    /// <summary>
    /// Código OTP em texto puro.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Hash persistido do código OTP.
    /// </summary>
    public string Hash { get; init; } = string.Empty;
}
