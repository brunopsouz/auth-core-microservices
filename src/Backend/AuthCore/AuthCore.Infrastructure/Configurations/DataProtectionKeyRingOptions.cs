using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configuracoes do key ring compartilhado do Data Protection.
/// </summary>
internal sealed class DataProtectionKeyRingOptions
{
    /// <summary>
    /// Nome da secao de configuracao.
    /// </summary>
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Nome logico compartilhado pelas instancias do AuthCore.
    /// </summary>
    [Required]
    public string ApplicationName { get; init; } = "AuthCore";

    /// <summary>
    /// Nome da chave Redis que armazena o key ring.
    /// </summary>
    [Required]
    public string KeyName { get; init; } = "data-protection-keys";

    /// <summary>
    /// Indica se um certificado de protecao em repouso e obrigatorio.
    /// </summary>
    public bool RequireCertificate { get; init; }

    /// <summary>
    /// Caminho do certificado PKCS#12 usado para proteger as chaves.
    /// </summary>
    public string? CertificatePath { get; init; }

    /// <summary>
    /// Senha do certificado PKCS#12.
    /// </summary>
    public string? CertificatePassword { get; init; }
}
