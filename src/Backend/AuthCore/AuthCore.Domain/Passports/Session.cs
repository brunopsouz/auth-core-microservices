using System.Security.Cryptography;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa uma sessão autenticada do usuário.
/// </summary>
public sealed class Session
{
    /// <summary>
    /// Identificador público da sessão.
    /// </summary>
    public string SessionId { get; private set; } = string.Empty;

    /// <summary>
    /// Identificador interno do usuário dono da sessão.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Data de criação da sessão em UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Data de expiração da sessão em UTC.
    /// </summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Data do último uso da sessão em UTC.
    /// </summary>
    public DateTime? LastSeenAtUtc { get; private set; }

    /// <summary>
    /// Endereço IP associado à sessão.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// User-Agent associado à sessão.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Data de revogação da sessão em UTC.
    /// </summary>
    public DateTime? RevokedAtUtc { get; private set; }


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private Session()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="sessionId">Identificador público da sessão.</param>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="createdAtUtc">Data de criação da sessão em UTC.</param>
    /// <param name="expiresAtUtc">Data de expiração da sessão em UTC.</param>
    /// <param name="lastSeenAtUtc">Data do último uso da sessão em UTC.</param>
    /// <param name="ipAddress">Endereço IP associado à sessão.</param>
    /// <param name="userAgent">User-Agent associado à sessão.</param>
    /// <param name="revokedAtUtc">Data de revogação da sessão em UTC.</param>
    private Session(
        string sessionId,
        Guid userId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? lastSeenAtUtc,
        string? ipAddress,
        string? userAgent,
        DateTime? revokedAtUtc)
    {
        SessionId = NormalizeSessionId(sessionId);
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        LastSeenAtUtc = lastSeenAtUtc;
        IpAddress = NormalizeOptional(ipAddress);
        UserAgent = NormalizeOptional(userAgent);
        RevokedAtUtc = revokedAtUtc;

        Validate();
    }



    /// <summary>
    /// Operação para emitir uma nova sessão autenticada.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="expiresAtUtc">Data de expiração da sessão em UTC.</param>
    /// <param name="ipAddress">Endereço IP associado à sessão.</param>
    /// <param name="userAgent">User-Agent associado à sessão.</param>
    /// <returns>Sessão autenticada emitida.</returns>
    public static Session Issue(
        Guid userId,
        DateTime expiresAtUtc,
        string? ipAddress,
        string? userAgent)
    {
        var nowUtc = DateTime.UtcNow;

        return new Session(
            CreateSessionId(),
            userId,
            nowUtc,
            expiresAtUtc,
            lastSeenAtUtc: nowUtc,
            ipAddress,
            userAgent,
            revokedAtUtc: null);
    }

    /// <summary>
    /// Operação para reconstruir uma sessão persistida.
    /// </summary>
    /// <param name="sessionId">Identificador público da sessão.</param>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="createdAtUtc">Data de criação da sessão em UTC.</param>
    /// <param name="expiresAtUtc">Data de expiração da sessão em UTC.</param>
    /// <param name="lastSeenAtUtc">Data do último uso da sessão em UTC.</param>
    /// <param name="ipAddress">Endereço IP associado à sessão.</param>
    /// <param name="userAgent">User-Agent associado à sessão.</param>
    /// <param name="revokedAtUtc">Data de revogação da sessão em UTC.</param>
    /// <returns>Sessão reconstruída.</returns>
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
        return new Session(
            sessionId,
            userId,
            createdAtUtc,
            expiresAtUtc,
            lastSeenAtUtc,
            ipAddress,
            userAgent,
            revokedAtUtc);
    }


    /// <summary>
    /// Operação para indicar se a sessão está utilizável no instante informado.
    /// </summary>
    /// <param name="referenceAtUtc">Data de referência em UTC.</param>
    /// <returns><c>true</c> quando a sessão está ativa; caso contrário, <c>false</c>.</returns>
    public bool IsAvailableAt(DateTime referenceAtUtc)
    {
        return RevokedAtUtc is null && ExpiresAtUtc > referenceAtUtc;
    }

    /// <summary>
    /// Operação para renovar o último uso da sessão.
    /// </summary>
    /// <param name="seenAtUtc">Data do uso em UTC.</param>
    /// <param name="expiresAtUtc">Nova data de expiração em UTC.</param>
    /// <returns>Nova instância de sessão atualizada.</returns>
    public Session Touch(DateTime seenAtUtc, DateTime expiresAtUtc)
    {
        DomainException.When(seenAtUtc == default, "A data de uso da sessão é obrigatória.");
        DomainException.When(expiresAtUtc <= seenAtUtc, "A expiração da sessão deve ser posterior ao último uso.");

        return new Session(
            SessionId,
            UserId,
            CreatedAtUtc,
            expiresAtUtc,
            seenAtUtc,
            IpAddress,
            UserAgent,
            RevokedAtUtc);
    }

    /// <summary>
    /// Operação para revogar a sessão.
    /// </summary>
    /// <param name="revokedAtUtc">Data da revogação em UTC.</param>
    /// <returns>Nova instância de sessão revogada.</returns>
    public Session Revoke(DateTime revokedAtUtc)
    {
        DomainException.When(revokedAtUtc == default, "A data de revogação da sessão é obrigatória.");

        return new Session(
            SessionId,
            UserId,
            CreatedAtUtc,
            ExpiresAtUtc,
            LastSeenAtUtc,
            IpAddress,
            UserAgent,
            revokedAtUtc);
    }


    /// <summary>
    /// Operação para validar a consistência da sessão.
    /// </summary>
    private void Validate()
    {
        DomainException.When(string.IsNullOrWhiteSpace(SessionId), "O identificador da sessão é obrigatório.");
        DomainException.When(UserId == Guid.Empty, "O identificador do usuário da sessão é obrigatório.");
        DomainException.When(CreatedAtUtc == default, "A data de criação da sessão é obrigatória.");
        DomainException.When(ExpiresAtUtc == default, "A data de expiração da sessão é obrigatória.");
        DomainException.When(ExpiresAtUtc <= CreatedAtUtc, "A expiração da sessão deve ser posterior à criação.");

        if (LastSeenAtUtc.HasValue)
            DomainException.When(LastSeenAtUtc.Value < CreatedAtUtc, "O último uso da sessão não pode ser anterior à criação.");

        if (RevokedAtUtc.HasValue)
            DomainException.When(RevokedAtUtc.Value < CreatedAtUtc, "A revogação da sessão não pode ser anterior à criação.");
    }

    /// <summary>
    /// Operação para gerar um identificador seguro para a sessão.
    /// </summary>
    /// <returns>Identificador codificado da sessão.</returns>
    private static string CreateSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Operação para normalizar o identificador da sessão.
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
    /// Operação para normalizar valores opcionais de texto.
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
