using AuthCore.Domain.Common;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa um token de renovação de sessão.
/// </summary>
public sealed class RefreshToken : AggregateRoot
{
    /// <summary>
    /// Identificador interno do usuário dono do token.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Identificador da família de rotação da sessão.
    /// </summary>
    public Guid FamilyId { get; private set; }

    /// <summary>
    /// Identificador do token anterior na cadeia.
    /// </summary>
    public Guid? ParentTokenId { get; private set; }

    /// <summary>
    /// Identificador do token emitido para substituir o atual.
    /// </summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>
    /// Hash persistido do segredo opaco do refresh token.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>
    /// Data de expiração do refresh token em UTC.
    /// </summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Data de consumo do refresh token em UTC.
    /// </summary>
    public DateTime? ConsumedAtUtc { get; private set; }

    /// <summary>
    /// Data de revogação do refresh token em UTC.
    /// </summary>
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>
    /// Motivo operacional da revogação.
    /// </summary>
    public string? RevocationReason { get; private set; }


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private RefreshToken()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="familyId">Identificador da família de rotação.</param>
    /// <param name="parentTokenId">Identificador do token anterior na cadeia.</param>
    /// <param name="replacedByTokenId">Identificador do token substituto.</param>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <param name="expiresAtUtc">Data de expiração do refresh token em UTC.</param>
    /// <param name="consumedAtUtc">Data de consumo do refresh token em UTC.</param>
    /// <param name="revokedAtUtc">Data de revogação do refresh token em UTC.</param>
    /// <param name="revocationReason">Motivo operacional da revogação.</param>
    private RefreshToken(
        Guid userId,
        Guid familyId,
        Guid? parentTokenId,
        Guid? replacedByTokenId,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime? consumedAtUtc,
        DateTime? revokedAtUtc,
        string? revocationReason)
    {
        UserId = userId;
        FamilyId = familyId;
        ParentTokenId = parentTokenId;
        ReplacedByTokenId = replacedByTokenId;
        TokenHash = NormalizeTokenHash(tokenHash);
        ExpiresAtUtc = expiresAtUtc;
        ConsumedAtUtc = consumedAtUtc;
        RevokedAtUtc = revokedAtUtc;
        RevocationReason = NormalizeReason(revocationReason);

        ValidateState(
            Id,
            UserId,
            FamilyId,
            ParentTokenId,
            ReplacedByTokenId,
            TokenHash,
            ExpiresAtUtc,
            ConsumedAtUtc,
            RevokedAtUtc,
            RevocationReason);
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador persistido do token.</param>
    /// <param name="createdAt">Data de criação persistida.</param>
    /// <param name="updateAt">Data da última atualização persistida.</param>
    /// <param name="isActive">Status de atividade persistido.</param>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="familyId">Identificador da família de rotação.</param>
    /// <param name="parentTokenId">Identificador do token anterior na cadeia.</param>
    /// <param name="replacedByTokenId">Identificador do token substituto.</param>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <param name="expiresAtUtc">Data de expiração do refresh token em UTC.</param>
    /// <param name="consumedAtUtc">Data de consumo do refresh token em UTC.</param>
    /// <param name="revokedAtUtc">Data de revogação do refresh token em UTC.</param>
    /// <param name="revocationReason">Motivo operacional da revogação.</param>
    private RefreshToken(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive,
        Guid userId,
        Guid familyId,
        Guid? parentTokenId,
        Guid? replacedByTokenId,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime? consumedAtUtc,
        DateTime? revokedAtUtc,
        string? revocationReason)
        : base(id, createdAt, updateAt, isActive)
    {
        UserId = userId;
        FamilyId = familyId;
        ParentTokenId = parentTokenId;
        ReplacedByTokenId = replacedByTokenId;
        TokenHash = NormalizeTokenHash(tokenHash);
        ExpiresAtUtc = expiresAtUtc;
        ConsumedAtUtc = consumedAtUtc;
        RevokedAtUtc = revokedAtUtc;
        RevocationReason = NormalizeReason(revocationReason);

        ValidateState(
            Id,
            UserId,
            FamilyId,
            ParentTokenId,
            ReplacedByTokenId,
            TokenHash,
            ExpiresAtUtc,
            ConsumedAtUtc,
            RevokedAtUtc,
            RevocationReason);
    }



    /// <summary>
    /// Operação para emitir o primeiro refresh token de uma sessão.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <param name="expiresAtUtc">Data de expiração do refresh token em UTC.</param>
    /// <returns>Instância inicial de <see cref="RefreshToken"/>.</returns>
    public static RefreshToken IssueInitial(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc)
    {
        return new RefreshToken(
            userId: userId,
            familyId: Guid.NewGuid(),
            parentTokenId: null,
            replacedByTokenId: null,
            tokenHash: tokenHash,
            expiresAtUtc: expiresAtUtc,
            consumedAtUtc: null,
            revokedAtUtc: null,
            revocationReason: null);
    }

    /// <summary>
    /// Operação para emitir um refresh token sucessor na mesma família.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="familyId">Identificador da família de rotação.</param>
    /// <param name="parentTokenId">Identificador do token anterior na cadeia.</param>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <param name="expiresAtUtc">Data de expiração do refresh token em UTC.</param>
    /// <returns>Instância sucessora de <see cref="RefreshToken"/>.</returns>
    public static RefreshToken IssueReplacement(
        Guid userId,
        Guid familyId,
        Guid parentTokenId,
        string tokenHash,
        DateTime expiresAtUtc)
    {
        return new RefreshToken(
            userId: userId,
            familyId: familyId,
            parentTokenId: parentTokenId,
            replacedByTokenId: null,
            tokenHash: tokenHash,
            expiresAtUtc: expiresAtUtc,
            consumedAtUtc: null,
            revokedAtUtc: null,
            revocationReason: null);
    }

    /// <summary>
    /// Operação para reconstruir um refresh token persistido.
    /// </summary>
    /// <param name="id">Identificador persistido do token.</param>
    /// <param name="createdAt">Data de criação persistida.</param>
    /// <param name="updateAt">Data da última atualização persistida.</param>
    /// <param name="isActive">Status de atividade persistido.</param>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="familyId">Identificador da família de rotação.</param>
    /// <param name="parentTokenId">Identificador do token anterior na cadeia.</param>
    /// <param name="replacedByTokenId">Identificador do token substituto.</param>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <param name="expiresAtUtc">Data de expiração do refresh token em UTC.</param>
    /// <param name="consumedAtUtc">Data de consumo do refresh token em UTC.</param>
    /// <param name="revokedAtUtc">Data de revogação do refresh token em UTC.</param>
    /// <param name="revocationReason">Motivo operacional da revogação.</param>
    /// <returns>Instância restaurada de <see cref="RefreshToken"/>.</returns>
    public static RefreshToken Restore(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive,
        Guid userId,
        Guid familyId,
        Guid? parentTokenId,
        Guid? replacedByTokenId,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime? consumedAtUtc,
        DateTime? revokedAtUtc,
        string? revocationReason)
    {
        return new RefreshToken(
            id,
            createdAt,
            updateAt,
            isActive,
            userId,
            familyId,
            parentTokenId,
            replacedByTokenId,
            tokenHash,
            expiresAtUtc,
            consumedAtUtc,
            revokedAtUtc,
            revocationReason);
    }


    /// <summary>
    /// Operação para consumir o refresh token na rotação da sessão.
    /// </summary>
    /// <param name="replacedByTokenId">Identificador do token substituto.</param>
    /// <param name="consumedAtUtc">Data de consumo em UTC.</param>
    /// <returns>Instância atualizada de <see cref="RefreshToken"/>.</returns>
    public RefreshToken Consume(Guid replacedByTokenId, DateTime consumedAtUtc)
    {
        DomainException.When(replacedByTokenId == Guid.Empty, "O identificador do token substituto é obrigatório.");
        DomainException.When(consumedAtUtc == default, "A data de consumo do refresh token é obrigatória.");
        DomainException.When(ConsumedAtUtc.HasValue, "O refresh token informado já foi consumido.");
        DomainException.When(RevokedAtUtc.HasValue, "O refresh token revogado não pode ser consumido.");
        DomainException.When(!IsActive, "O refresh token inativo não pode ser consumido.");
        DomainException.When(consumedAtUtc >= ExpiresAtUtc, "O refresh token expirado não pode ser consumido.");

        return Restore(
            Id,
            CreatedAt,
            DateTime.Now,
            IsActive,
            UserId,
            FamilyId,
            ParentTokenId,
            replacedByTokenId,
            TokenHash,
            ExpiresAtUtc,
            consumedAtUtc,
            RevokedAtUtc,
            RevocationReason);
    }

    /// <summary>
    /// Operação para revogar o refresh token.
    /// </summary>
    /// <param name="reason">Motivo operacional da revogação.</param>
    /// <param name="revokedAtUtc">Data de revogação em UTC.</param>
    /// <returns>Instância atualizada de <see cref="RefreshToken"/>.</returns>
    public RefreshToken Revoke(string reason, DateTime revokedAtUtc)
    {
        DomainException.When(string.IsNullOrWhiteSpace(reason), "O motivo da revogação do refresh token é obrigatório.");
        DomainException.When(revokedAtUtc == default, "A data de revogação do refresh token é obrigatória.");

        if (RevokedAtUtc.HasValue)
            return this;

        return Restore(
            Id,
            CreatedAt,
            DateTime.Now,
            IsActive,
            UserId,
            FamilyId,
            ParentTokenId,
            ReplacedByTokenId,
            TokenHash,
            ExpiresAtUtc,
            ConsumedAtUtc,
            revokedAtUtc,
            reason);
    }

    /// <summary>
    /// Operação para verificar se o refresh token está ativo no instante informado.
    /// </summary>
    /// <param name="nowUtc">Instante de referência em UTC.</param>
    /// <returns><c>true</c> quando o token está apto para uso; caso contrário, <c>false</c>.</returns>
    public bool IsActiveAt(DateTime nowUtc)
    {
        DomainException.When(nowUtc == default, "O instante de referência do refresh token é obrigatório.");

        return IsActive
            && !ConsumedAtUtc.HasValue
            && !RevokedAtUtc.HasValue
            && ExpiresAtUtc > nowUtc;
    }

    /// <summary>
    /// Operação para indicar se o refresh token ainda não foi consumido em uma rotação.
    /// </summary>
    /// <returns><c>true</c> quando o token ainda não foi consumido; caso contrário, <c>false</c>.</returns>
    public bool IsReusable()
    {
        return !ConsumedAtUtc.HasValue;
    }


    /// <summary>
    /// Operação para validar a consistência do estado interno do refresh token.
    /// </summary>
    /// <param name="id">Identificador do refresh token.</param>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="familyId">Identificador da família de rotação.</param>
    /// <param name="parentTokenId">Identificador do token anterior na cadeia.</param>
    /// <param name="replacedByTokenId">Identificador do token substituto.</param>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <param name="expiresAtUtc">Data de expiração do refresh token em UTC.</param>
    /// <param name="consumedAtUtc">Data de consumo do refresh token em UTC.</param>
    /// <param name="revokedAtUtc">Data de revogação do refresh token em UTC.</param>
    /// <param name="revocationReason">Motivo operacional da revogação.</param>
    private static void ValidateState(
        Guid id,
        Guid userId,
        Guid familyId,
        Guid? parentTokenId,
        Guid? replacedByTokenId,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime? consumedAtUtc,
        DateTime? revokedAtUtc,
        string? revocationReason)
    {
        DomainException.When(id == Guid.Empty, "O identificador do refresh token é obrigatório.");
        DomainException.When(userId == Guid.Empty, "O identificador do usuário do refresh token é obrigatório.");
        DomainException.When(familyId == Guid.Empty, "O identificador da família do refresh token é obrigatório.");
        DomainException.When(parentTokenId.HasValue && parentTokenId.Value == Guid.Empty, "O identificador do token pai é inválido.");
        DomainException.When(replacedByTokenId.HasValue && replacedByTokenId.Value == Guid.Empty, "O identificador do token substituto é inválido.");
        DomainException.When(parentTokenId.HasValue && parentTokenId.Value == id, "O refresh token não pode apontar para si mesmo como token pai.");
        DomainException.When(replacedByTokenId.HasValue && replacedByTokenId.Value == id, "O refresh token não pode apontar para si mesmo como token substituto.");
        DomainException.When(parentTokenId.HasValue && replacedByTokenId.HasValue && parentTokenId.Value == replacedByTokenId.Value, "Os identificadores do token pai e do token substituto devem ser diferentes.");
        DomainException.When(string.IsNullOrWhiteSpace(tokenHash), "O hash do refresh token é obrigatório.");
        DomainException.When(expiresAtUtc == default, "A data de expiração do refresh token é obrigatória.");
        DomainException.When(consumedAtUtc.HasValue && consumedAtUtc.Value == default, "A data de consumo do refresh token é inválida.");
        DomainException.When(consumedAtUtc.HasValue && !replacedByTokenId.HasValue, "O token substituto deve ser informado quando o refresh token é consumido.");
        DomainException.When(replacedByTokenId.HasValue && !consumedAtUtc.HasValue, "A data de consumo deve ser informada quando existe token substituto.");
        DomainException.When(consumedAtUtc.HasValue && consumedAtUtc.Value >= expiresAtUtc, "O refresh token não pode ser consumido após a expiração.");
        DomainException.When(revokedAtUtc.HasValue && revokedAtUtc.Value == default, "A data de revogação do refresh token é inválida.");
        DomainException.When(revokedAtUtc.HasValue && string.IsNullOrWhiteSpace(revocationReason), "O motivo da revogação do refresh token é obrigatório.");
        DomainException.When(!revokedAtUtc.HasValue && !string.IsNullOrWhiteSpace(revocationReason), "A data de revogação do refresh token é obrigatória quando existe motivo de revogação.");
    }

    /// <summary>
    /// Operação para normalizar o hash persistido do refresh token.
    /// </summary>
    /// <param name="tokenHash">Hash persistido do refresh token.</param>
    /// <returns>Hash normalizado.</returns>
    private static string NormalizeTokenHash(string tokenHash)
    {
        return tokenHash.Trim();
    }

    /// <summary>
    /// Operação para normalizar o motivo operacional da revogação.
    /// </summary>
    /// <param name="reason">Motivo operacional da revogação.</param>
    /// <returns>Motivo normalizado ou nulo.</returns>
    private static string? NormalizeReason(string? reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim();
    }

}
