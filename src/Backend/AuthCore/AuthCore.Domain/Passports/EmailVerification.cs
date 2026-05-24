using System.Security.Cryptography;
using AuthCore.Domain.Common;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa a verificação de e-mail pendente do usuário.
/// </summary>
public sealed class EmailVerification : AggregateRoot
{
    private const int CODE_LENGTH = 6;

    /// <summary>
    /// Identificador interno do usuário.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// E-mail em verificação.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Hash persistido do código OTP.
    /// </summary>
    public string CodeHash { get; private set; } = string.Empty;

    /// <summary>
    /// Data de expiração do código em UTC.
    /// </summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Quantidade de tentativas inválidas registradas.
    /// </summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// Quantidade máxima de tentativas permitidas.
    /// </summary>
    public int MaxAttempts { get; private set; }

    /// <summary>
    /// Data limite para novo reenvio em UTC.
    /// </summary>
    public DateTime? CooldownUntilUtc { get; private set; }

    /// <summary>
    /// Data do último envio em UTC.
    /// </summary>
    public DateTime LastSentAtUtc { get; private set; }

    /// <summary>
    /// Data de consumo do código em UTC.
    /// </summary>
    public DateTime? ConsumedAtUtc { get; private set; }

    /// <summary>
    /// Data de revogação da verificação em UTC.
    /// </summary>
    public DateTime? RevokedAtUtc { get; private set; }


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private EmailVerification()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="email">E-mail em verificação.</param>
    /// <param name="codeHash">Hash persistido do código.</param>
    /// <param name="expiresAtUtc">Data de expiração do código em UTC.</param>
    /// <param name="attemptCount">Quantidade de tentativas inválidas.</param>
    /// <param name="maxAttempts">Quantidade máxima de tentativas.</param>
    /// <param name="cooldownUntilUtc">Data limite para novo reenvio em UTC.</param>
    /// <param name="lastSentAtUtc">Data do último envio em UTC.</param>
    /// <param name="consumedAtUtc">Data de consumo do código em UTC.</param>
    /// <param name="revokedAtUtc">Data de revogação da verificação em UTC.</param>
    private EmailVerification(
        Guid userId,
        string email,
        string codeHash,
        DateTime expiresAtUtc,
        int attemptCount,
        int maxAttempts,
        DateTime? cooldownUntilUtc,
        DateTime lastSentAtUtc,
        DateTime? consumedAtUtc,
        DateTime? revokedAtUtc)
    {
        UserId = userId;
        Email = NormalizeEmail(email);
        CodeHash = NormalizeHash(codeHash);
        ExpiresAtUtc = expiresAtUtc;
        AttemptCount = attemptCount;
        MaxAttempts = maxAttempts;
        CooldownUntilUtc = cooldownUntilUtc;
        LastSentAtUtc = lastSentAtUtc;
        ConsumedAtUtc = consumedAtUtc;
        RevokedAtUtc = revokedAtUtc;

        Validate();
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador persistido da verificação.</param>
    /// <param name="createdAt">Data de criação persistida.</param>
    /// <param name="updateAt">Data da última atualização persistida.</param>
    /// <param name="isActive">Status de atividade persistido.</param>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="email">E-mail em verificação.</param>
    /// <param name="codeHash">Hash persistido do código.</param>
    /// <param name="expiresAtUtc">Data de expiração do código em UTC.</param>
    /// <param name="attemptCount">Quantidade de tentativas inválidas.</param>
    /// <param name="maxAttempts">Quantidade máxima de tentativas.</param>
    /// <param name="cooldownUntilUtc">Data limite para novo reenvio em UTC.</param>
    /// <param name="lastSentAtUtc">Data do último envio em UTC.</param>
    /// <param name="consumedAtUtc">Data de consumo do código em UTC.</param>
    /// <param name="revokedAtUtc">Data de revogação da verificação em UTC.</param>
    private EmailVerification(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive,
        Guid userId,
        string email,
        string codeHash,
        DateTime expiresAtUtc,
        int attemptCount,
        int maxAttempts,
        DateTime? cooldownUntilUtc,
        DateTime lastSentAtUtc,
        DateTime? consumedAtUtc,
        DateTime? revokedAtUtc)
        : base(id, createdAt, updateAt, isActive)
    {
        UserId = userId;
        Email = NormalizeEmail(email);
        CodeHash = NormalizeHash(codeHash);
        ExpiresAtUtc = expiresAtUtc;
        AttemptCount = attemptCount;
        MaxAttempts = maxAttempts;
        CooldownUntilUtc = cooldownUntilUtc;
        LastSentAtUtc = lastSentAtUtc;
        ConsumedAtUtc = consumedAtUtc;
        RevokedAtUtc = revokedAtUtc;

        Validate();
    }



    /// <summary>
    /// Operação para emitir uma nova verificação de e-mail.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="email">E-mail em verificação.</param>
    /// <param name="codeHash">Hash persistido do código.</param>
    /// <param name="expiresAtUtc">Data de expiração do código em UTC.</param>
    /// <param name="maxAttempts">Quantidade máxima de tentativas.</param>
    /// <param name="cooldownUntilUtc">Data limite para novo reenvio em UTC.</param>
    /// <param name="sentAtUtc">Data do envio em UTC.</param>
    /// <returns>Verificação emitida.</returns>
    public static EmailVerification Issue(
        Guid userId,
        string email,
        string codeHash,
        DateTime expiresAtUtc,
        int maxAttempts,
        DateTime? cooldownUntilUtc,
        DateTime sentAtUtc)
    {
        return new EmailVerification(
            userId,
            email,
            codeHash,
            expiresAtUtc,
            attemptCount: 0,
            maxAttempts,
            cooldownUntilUtc,
            sentAtUtc,
            consumedAtUtc: null,
            revokedAtUtc: null);
    }

    /// <summary>
    /// Operação para reconstruir uma verificação persistida.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="email">E-mail em verificação.</param>
    /// <param name="codeHash">Hash persistido do código.</param>
    /// <param name="expiresAtUtc">Data de expiração do código em UTC.</param>
    /// <param name="attemptCount">Quantidade de tentativas inválidas.</param>
    /// <param name="maxAttempts">Quantidade máxima de tentativas.</param>
    /// <param name="cooldownUntilUtc">Data limite para novo reenvio em UTC.</param>
    /// <param name="lastSentAtUtc">Data do último envio em UTC.</param>
    /// <param name="consumedAtUtc">Data de consumo do código em UTC.</param>
    /// <param name="revokedAtUtc">Data de revogação da verificação em UTC.</param>
    /// <returns>Verificação reconstruída.</returns>
    public static EmailVerification Restore(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive,
        Guid userId,
        string email,
        string codeHash,
        DateTime expiresAtUtc,
        int attemptCount,
        int maxAttempts,
        DateTime? cooldownUntilUtc,
        DateTime lastSentAtUtc,
        DateTime? consumedAtUtc,
        DateTime? revokedAtUtc)
    {
        return new EmailVerification(
            id,
            createdAt,
            updateAt,
            isActive,
            userId,
            email,
            codeHash,
            expiresAtUtc,
            attemptCount,
            maxAttempts,
            cooldownUntilUtc,
            lastSentAtUtc,
            consumedAtUtc,
            revokedAtUtc);
    }


    /// <summary>
    /// Operação para indicar se a verificação está ativa no instante informado.
    /// </summary>
    /// <param name="referenceAtUtc">Data de referência em UTC.</param>
    /// <returns><c>true</c> quando a verificação está ativa; caso contrário, <c>false</c>.</returns>
    public bool IsActiveAt(DateTime referenceAtUtc)
    {
        return RevokedAtUtc is null
            && ConsumedAtUtc is null
            && AttemptCount < MaxAttempts
            && ExpiresAtUtc > referenceAtUtc;
    }

    /// <summary>
    /// Operação para indicar se o reenvio ainda está em cooldown.
    /// </summary>
    /// <param name="referenceAtUtc">Data de referência em UTC.</param>
    /// <returns><c>true</c> quando o reenvio deve aguardar; caso contrário, <c>false</c>.</returns>
    public bool IsInCooldownAt(DateTime referenceAtUtc)
    {
        return CooldownUntilUtc.HasValue && CooldownUntilUtc.Value > referenceAtUtc;
    }

    /// <summary>
    /// Operação para validar o código informado.
    /// </summary>
    /// <param name="providedCodeHash">Hash do código informado.</param>
    /// <param name="validatedAtUtc">Data da validação em UTC.</param>
    /// <returns>Nova instância representando o resultado da validação.</returns>
    public EmailVerification ValidateCode(string providedCodeHash, DateTime validatedAtUtc)
    {
        DomainException.When(validatedAtUtc == default, "A data de validação do código é obrigatória.");
        DomainException.When(!IsActiveAt(validatedAtUtc), "O código de verificação é inválido ou expirou.");

        if (CodeHash == NormalizeHash(providedCodeHash))
        {
            return Restore(
                Id,
                CreatedAt,
                validatedAtUtc,
                IsActive,
                UserId,
                Email,
                CodeHash,
                ExpiresAtUtc,
                AttemptCount,
                MaxAttempts,
                CooldownUntilUtc,
                LastSentAtUtc,
                validatedAtUtc,
                RevokedAtUtc);
        }

        var updatedAttempts = AttemptCount + 1;
        var revokedAtUtc = updatedAttempts >= MaxAttempts
            ? validatedAtUtc
            : RevokedAtUtc;

        return Restore(
            Id,
            CreatedAt,
            validatedAtUtc,
            IsActive,
            UserId,
            Email,
            CodeHash,
            ExpiresAtUtc,
            updatedAttempts,
            MaxAttempts,
            CooldownUntilUtc,
            LastSentAtUtc,
            ConsumedAtUtc,
            revokedAtUtc);
    }

    /// <summary>
    /// Operação para revogar a verificação atual.
    /// </summary>
    /// <param name="revokedAtUtc">Data da revogação em UTC.</param>
    /// <returns>Nova instância revogada.</returns>
    public EmailVerification Revoke(DateTime revokedAtUtc)
    {
        DomainException.When(revokedAtUtc == default, "A data de revogação da verificação é obrigatória.");

        return Restore(
            Id,
            CreatedAt,
            revokedAtUtc,
            IsActive,
            UserId,
            Email,
            CodeHash,
            ExpiresAtUtc,
            AttemptCount,
            MaxAttempts,
            CooldownUntilUtc,
            LastSentAtUtc,
            ConsumedAtUtc,
            revokedAtUtc);
    }

    /// <summary>
    /// Operação para reemitir a verificação preservando a identidade persistida.
    /// </summary>
    /// <param name="codeHash">Novo hash persistido do código.</param>
    /// <param name="expiresAtUtc">Nova data de expiração do código em UTC.</param>
    /// <param name="maxAttempts">Nova quantidade máxima de tentativas.</param>
    /// <param name="cooldownUntilUtc">Nova data limite para reenvio em UTC.</param>
    /// <param name="sentAtUtc">Nova data de envio em UTC.</param>
    /// <returns>Nova instância reemitida.</returns>
    public EmailVerification Reissue(
        string codeHash,
        DateTime expiresAtUtc,
        int maxAttempts,
        DateTime? cooldownUntilUtc,
        DateTime sentAtUtc)
    {
        DomainException.When(sentAtUtc == default, "A data de envio do código é obrigatória.");

        return Restore(
            Id,
            CreatedAt,
            sentAtUtc,
            IsActive,
            UserId,
            Email,
            codeHash,
            expiresAtUtc,
            attemptCount: 0,
            maxAttempts,
            cooldownUntilUtc,
            sentAtUtc,
            consumedAtUtc: null,
            revokedAtUtc: null);
    }

    /// <summary>
    /// Operação para gerar um novo código OTP numérico.
    /// </summary>
    /// <returns>Código OTP em texto puro.</returns>
    public static string GenerateCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString($"D{CODE_LENGTH}");
    }


    /// <summary>
    /// Operação para validar a consistência da verificação.
    /// </summary>
    private void Validate()
    {
        DomainException.When(Id == Guid.Empty, "O identificador da verificação é obrigatório.");
        DomainException.When(UserId == Guid.Empty, "O identificador do usuário é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(Email), "O e-mail da verificação é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(CodeHash), "O hash do código de verificação é obrigatório.");
        DomainException.When(ExpiresAtUtc == default, "A expiração do código de verificação é obrigatória.");
        DomainException.When(AttemptCount < 0, "A quantidade de tentativas não pode ser negativa.");
        DomainException.When(MaxAttempts <= 0, "A quantidade máxima de tentativas deve ser maior que zero.");
        DomainException.When(AttemptCount > MaxAttempts, "A quantidade de tentativas não pode ultrapassar o limite máximo.");
        DomainException.When(LastSentAtUtc == default, "A data de envio do código é obrigatória.");
        DomainException.When(ExpiresAtUtc <= LastSentAtUtc, "A expiração do código deve ser posterior ao envio.");
    }

    /// <summary>
    /// Operação para normalizar o e-mail persistido.
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    /// <returns>E-mail normalizado.</returns>
    private static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Operação para normalizar o hash persistido.
    /// </summary>
    /// <param name="codeHash">Hash informado.</param>
    /// <returns>Hash normalizado.</returns>
    private static string NormalizeHash(string codeHash)
    {
        return string.IsNullOrWhiteSpace(codeHash)
            ? string.Empty
            : codeHash.Trim();
    }

}
