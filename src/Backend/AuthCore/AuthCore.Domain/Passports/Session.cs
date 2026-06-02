using System.Security.Cryptography;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Users;

namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa uma sessao autenticada duravel do usuario.
/// </summary>
public sealed class Session
{
    /// <summary>
    /// Identificador opaco e secreto usado no cookie da sessao.
    /// </summary>
    public SessionIdentifier Identifier { get; private set; } = null!;

    /// <summary>
    /// Identificador publico nao secreto da sessao.
    /// </summary>
    public string PublicSessionId { get; private set; } = string.Empty;

    /// <summary>
    /// Identificador opaco e secreto usado no cookie da sessao.
    /// </summary>
    public string SessionId => Identifier.Value;

    /// <summary>
    /// Identificador interno do usuario dono da sessao.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Status persistido da sessao.
    /// </summary>
    public SessionStatus Status { get; private set; }

    /// <summary>
    /// Carimbo de seguranca do usuario no momento da emissao da sessao.
    /// </summary>
    public SecurityStamp SecurityStamp { get; private set; } = null!;

    /// <summary>
    /// Data de criacao da sessao em UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Data de expiracao da sessao em UTC.
    /// </summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Data do ultimo uso da sessao em UTC.
    /// </summary>
    public DateTime? LastSeenAtUtc { get; private set; }

    /// <summary>
    /// Endereco IP associado a sessao.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// User-Agent associado a sessao.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Data de revogacao da sessao em UTC.
    /// </summary>
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>
    /// Motivo da revogacao da sessao.
    /// </summary>
    public SessionRevocationReason? RevocationReason { get; private set; }


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    private Session()
    {
    }

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="identifier">Identificador opaco da sessao.</param>
    /// <param name="publicSessionId">Identificador publico da sessao.</param>
    /// <param name="userId">Identificador interno do usuario.</param>
    /// <param name="status">Status persistido da sessao.</param>
    /// <param name="securityStamp">Carimbo de seguranca da sessao.</param>
    /// <param name="createdAtUtc">Data de criacao da sessao em UTC.</param>
    /// <param name="expiresAtUtc">Data de expiracao da sessao em UTC.</param>
    /// <param name="lastSeenAtUtc">Data do ultimo uso da sessao em UTC.</param>
    /// <param name="ipAddress">Endereco IP associado a sessao.</param>
    /// <param name="userAgent">User-Agent associado a sessao.</param>
    /// <param name="revokedAtUtc">Data de revogacao da sessao em UTC.</param>
    /// <param name="revocationReason">Motivo da revogacao da sessao.</param>
    private Session(
        SessionIdentifier identifier,
        string publicSessionId,
        Guid userId,
        SessionStatus status,
        SecurityStamp securityStamp,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? lastSeenAtUtc,
        string? ipAddress,
        string? userAgent,
        DateTime? revokedAtUtc,
        SessionRevocationReason? revocationReason)
    {
        Identifier = identifier;
        PublicSessionId = NormalizePublicSessionId(publicSessionId);
        UserId = userId;
        Status = status;
        SecurityStamp = securityStamp;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        LastSeenAtUtc = lastSeenAtUtc;
        IpAddress = NormalizeOptional(ipAddress);
        UserAgent = NormalizeOptional(userAgent);
        RevokedAtUtc = revokedAtUtc;
        RevocationReason = revocationReason;

        Validate();
    }


    /// <summary>
    /// Operacao para emitir uma nova sessao autenticada.
    /// </summary>
    /// <param name="userId">Identificador interno do usuario.</param>
    /// <param name="securityStamp">Carimbo de seguranca atual do usuario.</param>
    /// <param name="expiresAtUtc">Data de expiracao da sessao em UTC.</param>
    /// <param name="ipAddress">Endereco IP associado a sessao.</param>
    /// <param name="userAgent">User-Agent associado a sessao.</param>
    /// <returns>Sessao autenticada emitida.</returns>
    public static Session Issue(
        Guid userId,
        SecurityStamp securityStamp,
        DateTime expiresAtUtc,
        string? ipAddress,
        string? userAgent)
    {
        var nowUtc = DateTime.UtcNow;

        return new Session(
            SessionIdentifier.Create(CreateOpaqueSessionId()),
            CreatePublicSessionId(),
            userId,
            SessionStatus.Active,
            securityStamp,
            nowUtc,
            expiresAtUtc,
            lastSeenAtUtc: nowUtc,
            ipAddress,
            userAgent,
            revokedAtUtc: null,
            revocationReason: null);
    }

    /// <summary>
    /// Operacao para emitir uma nova sessao autenticada.
    /// </summary>
    /// <param name="userId">Identificador interno do usuario.</param>
    /// <param name="expiresAtUtc">Data de expiracao da sessao em UTC.</param>
    /// <param name="ipAddress">Endereco IP associado a sessao.</param>
    /// <param name="userAgent">User-Agent associado a sessao.</param>
    /// <returns>Sessao autenticada emitida.</returns>
    public static Session Issue(
        Guid userId,
        DateTime expiresAtUtc,
        string? ipAddress,
        string? userAgent)
    {
        return Issue(userId, SecurityStamp.Create(), expiresAtUtc, ipAddress, userAgent);
    }

    /// <summary>
    /// Operacao para reconstruir uma sessao persistida.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="userId">Identificador interno do usuario.</param>
    /// <param name="createdAtUtc">Data de criacao da sessao em UTC.</param>
    /// <param name="expiresAtUtc">Data de expiracao da sessao em UTC.</param>
    /// <param name="lastSeenAtUtc">Data do ultimo uso da sessao em UTC.</param>
    /// <param name="ipAddress">Endereco IP associado a sessao.</param>
    /// <param name="userAgent">User-Agent associado a sessao.</param>
    /// <param name="revokedAtUtc">Data de revogacao da sessao em UTC.</param>
    /// <returns>Sessao reconstruida.</returns>
    public static Session Restore(
        string sessionId,
        Guid userId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? lastSeenAtUtc,
        string? ipAddress,
        string? userAgent,
        DateTime? revokedAtUtc)
    {
        var status = revokedAtUtc.HasValue
            ? SessionStatus.Revoked
            : SessionStatus.Active;

        return Restore(
            sessionId,
            publicSessionId: sessionId,
            userId,
            status,
            SecurityStamp.Create().Value,
            createdAtUtc,
            expiresAtUtc,
            lastSeenAtUtc,
            ipAddress,
            userAgent,
            revokedAtUtc,
            revokedAtUtc.HasValue ? SessionRevocationReason.UserLogout : null);
    }

    /// <summary>
    /// Operacao para reconstruir uma sessao persistida.
    /// </summary>
    /// <param name="sessionId">Identificador opaco da sessao.</param>
    /// <param name="publicSessionId">Identificador publico da sessao.</param>
    /// <param name="userId">Identificador interno do usuario.</param>
    /// <param name="status">Status persistido da sessao.</param>
    /// <param name="securityStamp">Carimbo de seguranca persistido.</param>
    /// <param name="createdAtUtc">Data de criacao da sessao em UTC.</param>
    /// <param name="expiresAtUtc">Data de expiracao da sessao em UTC.</param>
    /// <param name="lastSeenAtUtc">Data do ultimo uso da sessao em UTC.</param>
    /// <param name="ipAddress">Endereco IP associado a sessao.</param>
    /// <param name="userAgent">User-Agent associado a sessao.</param>
    /// <param name="revokedAtUtc">Data de revogacao da sessao em UTC.</param>
    /// <param name="revocationReason">Motivo da revogacao da sessao.</param>
    /// <returns>Sessao reconstruida.</returns>
    public static Session Restore(
        string sessionId,
        string publicSessionId,
        Guid userId,
        SessionStatus status,
        string securityStamp,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? lastSeenAtUtc,
        string? ipAddress,
        string? userAgent,
        DateTime? revokedAtUtc,
        SessionRevocationReason? revocationReason)
    {
        return new Session(
            SessionIdentifier.Create(sessionId),
            publicSessionId,
            userId,
            status,
            SecurityStamp.Restore(securityStamp),
            createdAtUtc,
            expiresAtUtc,
            lastSeenAtUtc,
            ipAddress,
            userAgent,
            revokedAtUtc,
            revocationReason);
    }


    /// <summary>
    /// Operacao para indicar se a sessao esta utilizavel no instante informado.
    /// </summary>
    /// <param name="referenceAtUtc">Data de referencia em UTC.</param>
    /// <returns><c>true</c> quando a sessao esta ativa; caso contrario, <c>false</c>.</returns>
    public bool IsAvailableAt(DateTime referenceAtUtc)
    {
        DomainException.When(referenceAtUtc == default, "O instante de referencia da sessao e obrigatorio.");

        return Status == SessionStatus.Active
            && RevokedAtUtc is null
            && ExpiresAtUtc > referenceAtUtc;
    }

    /// <summary>
    /// Operacao para validar emissao de access token para a sessao.
    /// </summary>
    /// <param name="referenceAtUtc">Data de referencia em UTC.</param>
    /// <param name="currentSecurityStamp">Carimbo de seguranca atual do usuario.</param>
    public void EnsureCanIssueAccessToken(DateTime referenceAtUtc, SecurityStamp currentSecurityStamp)
    {
        ArgumentNullException.ThrowIfNull(currentSecurityStamp);
        DomainException.When(referenceAtUtc == default, "O instante de referencia da sessao e obrigatorio.");
        DomainException.When(Status != SessionStatus.Active, "A sessao nao esta ativa.");
        DomainException.When(RevokedAtUtc.HasValue, "A sessao revogada nao pode emitir access token.");
        DomainException.When(ExpiresAtUtc <= referenceAtUtc, "A sessao expirada nao pode emitir access token.");
        DomainException.When(!SecurityStamp.Equals(currentSecurityStamp), "O carimbo de seguranca da sessao diverge do usuario.");
    }

    /// <summary>
    /// Operacao para renovar o ultimo uso da sessao.
    /// </summary>
    /// <param name="seenAtUtc">Data do uso em UTC.</param>
    /// <param name="expiresAtUtc">Nova data de expiracao em UTC.</param>
    /// <returns>Nova instancia de sessao atualizada.</returns>
    public Session Touch(DateTime seenAtUtc, DateTime expiresAtUtc)
    {
        DomainException.When(seenAtUtc == default, "A data de uso da sessao e obrigatoria.");
        DomainException.When(seenAtUtc < CreatedAtUtc, "O ultimo uso da sessao nao pode ser anterior a criacao.");
        DomainException.When(expiresAtUtc <= seenAtUtc, "A expiracao da sessao deve ser posterior ao ultimo uso.");

        return new Session(
            Identifier,
            PublicSessionId,
            UserId,
            Status,
            SecurityStamp,
            CreatedAtUtc,
            expiresAtUtc,
            seenAtUtc,
            IpAddress,
            UserAgent,
            RevokedAtUtc,
            RevocationReason);
    }

    /// <summary>
    /// Operacao para revogar a sessao.
    /// </summary>
    /// <param name="reason">Motivo da revogacao.</param>
    /// <param name="revokedAtUtc">Data da revogacao em UTC.</param>
    /// <returns>Nova instancia de sessao revogada.</returns>
    public Session Revoke(SessionRevocationReason reason, DateTime revokedAtUtc)
    {
        DomainException.When(!Enum.IsDefined(typeof(SessionRevocationReason), reason), "O motivo de revogacao da sessao e invalido.");
        DomainException.When(revokedAtUtc == default, "A data de revogacao da sessao e obrigatoria.");

        if (Status == SessionStatus.Revoked)
            return this;

        return new Session(
            Identifier,
            PublicSessionId,
            UserId,
            SessionStatus.Revoked,
            SecurityStamp,
            CreatedAtUtc,
            ExpiresAtUtc,
            LastSeenAtUtc,
            IpAddress,
            UserAgent,
            revokedAtUtc,
            reason);
    }

    /// <summary>
    /// Operacao para revogar a sessao.
    /// </summary>
    /// <param name="revokedAtUtc">Data da revogacao em UTC.</param>
    /// <returns>Nova instancia de sessao revogada.</returns>
    public Session Revoke(DateTime revokedAtUtc)
    {
        return Revoke(SessionRevocationReason.UserLogout, revokedAtUtc);
    }


    /// <summary>
    /// Operacao para validar a consistencia da sessao.
    /// </summary>
    private void Validate()
    {
        DomainException.When(Identifier is null, "O identificador opaco da sessao e obrigatorio.");
        DomainException.When(string.IsNullOrWhiteSpace(PublicSessionId), "O identificador publico da sessao e obrigatorio.");
        DomainException.When(UserId == Guid.Empty, "O identificador do usuario da sessao e obrigatorio.");
        DomainException.When(!Enum.IsDefined(typeof(SessionStatus), Status), "Status da sessao invalido.");
        DomainException.When(SecurityStamp is null, "O carimbo de seguranca da sessao e obrigatorio.");
        DomainException.When(CreatedAtUtc == default, "A data de criacao da sessao e obrigatoria.");
        DomainException.When(ExpiresAtUtc == default, "A data de expiracao da sessao e obrigatoria.");
        DomainException.When(ExpiresAtUtc <= CreatedAtUtc, "A expiracao da sessao deve ser posterior a criacao.");

        if (LastSeenAtUtc.HasValue)
            DomainException.When(LastSeenAtUtc.Value < CreatedAtUtc, "O ultimo uso da sessao nao pode ser anterior a criacao.");

        if (RevokedAtUtc.HasValue)
            DomainException.When(RevokedAtUtc.Value < CreatedAtUtc, "A revogacao da sessao nao pode ser anterior a criacao.");

        DomainException.When(Status == SessionStatus.Revoked && !RevokedAtUtc.HasValue, "A sessao revogada deve possuir data de revogacao.");
        DomainException.When(Status == SessionStatus.Revoked && !RevocationReason.HasValue, "A sessao revogada deve possuir motivo de revogacao.");
        DomainException.When(Status != SessionStatus.Revoked && RevokedAtUtc.HasValue, "Apenas sessoes revogadas podem possuir data de revogacao.");
        DomainException.When(Status != SessionStatus.Revoked && RevocationReason.HasValue, "Apenas sessoes revogadas podem possuir motivo de revogacao.");
    }

    /// <summary>
    /// Operacao para gerar um identificador opaco seguro para a sessao.
    /// </summary>
    /// <returns>Identificador opaco codificado da sessao.</returns>
    private static string CreateOpaqueSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Operacao para gerar um identificador publico para a sessao.
    /// </summary>
    /// <returns>Identificador publico da sessao.</returns>
    private static string CreatePublicSessionId()
    {
        return $"sess_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Operacao para normalizar o identificador publico da sessao.
    /// </summary>
    /// <param name="publicSessionId">Identificador informado.</param>
    /// <returns>Identificador normalizado.</returns>
    private static string NormalizePublicSessionId(string publicSessionId)
    {
        return string.IsNullOrWhiteSpace(publicSessionId)
            ? string.Empty
            : publicSessionId.Trim();
    }

    /// <summary>
    /// Operacao para normalizar valores opcionais de texto.
    /// </summary>
    /// <param name="value">Valor informado.</param>
    /// <returns>Valor normalizado ou nulo.</returns>
    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
