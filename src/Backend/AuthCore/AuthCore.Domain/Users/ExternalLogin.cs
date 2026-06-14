using AuthCore.Domain.Common;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Domain.Users;

/// <summary>
/// Representa o vínculo entre usuário interno e login externo.
/// </summary>
public sealed class ExternalLogin : AggregateRoot
{
    /// <summary>
    /// Identificador interno do usuário vinculado.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Provedor externo de login.
    /// </summary>
    public ExternalLoginProvider Provider { get; private set; }

    /// <summary>
    /// Identificador do usuário no provedor externo.
    /// </summary>
    public string ProviderUserId { get; private set; } = string.Empty;

    /// <summary>
    /// E-mail informado pelo provedor externo.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Indica se o provedor externo confirmou o e-mail.
    /// </summary>
    public bool EmailVerified { get; private set; }

    /// <summary>
    /// Data de vínculo do login externo em UTC.
    /// </summary>
    public DateTime LinkedAtUtc { get; private set; }

    /// <summary>
    /// Data do último uso do login externo em UTC.
    /// </summary>
    public DateTime? LastUsedAtUtc { get; private set; }


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private ExternalLogin()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="provider">Provedor externo de login.</param>
    /// <param name="providerUserId">Identificador do usuário no provedor externo.</param>
    /// <param name="email">E-mail informado pelo provedor externo.</param>
    /// <param name="emailVerified">Indica se o e-mail foi confirmado pelo provedor externo.</param>
    /// <param name="linkedAtUtc">Data de vínculo em UTC.</param>
    /// <param name="lastUsedAtUtc">Data do último uso em UTC.</param>
    private ExternalLogin(
        Guid userId,
        ExternalLoginProvider provider,
        string providerUserId,
        string email,
        bool emailVerified,
        DateTime linkedAtUtc,
        DateTime? lastUsedAtUtc)
    {
        UserId = userId;
        Provider = provider;
        ProviderUserId = Normalize(providerUserId);
        Email = global::AuthCore.Domain.Users.Email.Create(email).Value;
        EmailVerified = emailVerified;
        LinkedAtUtc = linkedAtUtc;
        LastUsedAtUtc = lastUsedAtUtc;

        Validate();
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador persistido do vínculo externo.</param>
    /// <param name="createdAt">Data de criação persistida.</param>
    /// <param name="updateAt">Data da última atualização persistida.</param>
    /// <param name="isActive">Status de atividade persistido.</param>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="provider">Provedor externo de login.</param>
    /// <param name="providerUserId">Identificador do usuário no provedor externo.</param>
    /// <param name="email">E-mail informado pelo provedor externo.</param>
    /// <param name="emailVerified">Indica se o e-mail foi confirmado pelo provedor externo.</param>
    /// <param name="linkedAtUtc">Data de vínculo em UTC.</param>
    /// <param name="lastUsedAtUtc">Data do último uso em UTC.</param>
    private ExternalLogin(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive,
        Guid userId,
        ExternalLoginProvider provider,
        string providerUserId,
        string email,
        bool emailVerified,
        DateTime linkedAtUtc,
        DateTime? lastUsedAtUtc)
        : base(id, createdAt, updateAt, isActive)
    {
        UserId = userId;
        Provider = provider;
        ProviderUserId = Normalize(providerUserId);
        Email = global::AuthCore.Domain.Users.Email.Create(email).Value;
        EmailVerified = emailVerified;
        LinkedAtUtc = linkedAtUtc;
        LastUsedAtUtc = lastUsedAtUtc;

        Validate();
    }


    /// <summary>
    /// Operação para vincular login Google ao usuário.
    /// </summary>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="providerUserId">Identificador do usuário no Google.</param>
    /// <param name="email">E-mail informado pelo Google.</param>
    /// <param name="emailVerified">Indica se o Google confirmou o e-mail.</param>
    /// <param name="linkedAtUtc">Data de vínculo em UTC.</param>
    /// <returns>Vínculo externo criado.</returns>
    public static ExternalLogin LinkGoogle(
        Guid userId,
        string providerUserId,
        string email,
        bool emailVerified,
        DateTime linkedAtUtc)
    {
        return new ExternalLogin(
            userId,
            ExternalLoginProvider.Google,
            providerUserId,
            email,
            emailVerified,
            linkedAtUtc,
            lastUsedAtUtc: linkedAtUtc);
    }

    /// <summary>
    /// Operação para reconstruir um login externo persistido.
    /// </summary>
    /// <param name="id">Identificador persistido do vínculo externo.</param>
    /// <param name="createdAt">Data de criação persistida.</param>
    /// <param name="updateAt">Data da última atualização persistida.</param>
    /// <param name="isActive">Status de atividade persistido.</param>
    /// <param name="userId">Identificador interno do usuário.</param>
    /// <param name="provider">Provedor externo de login.</param>
    /// <param name="providerUserId">Identificador do usuário no provedor externo.</param>
    /// <param name="email">E-mail informado pelo provedor externo.</param>
    /// <param name="emailVerified">Indica se o provedor externo confirmou o e-mail.</param>
    /// <param name="linkedAtUtc">Data de vínculo em UTC.</param>
    /// <param name="lastUsedAtUtc">Data do último uso em UTC.</param>
    /// <returns>Vínculo externo reconstruído.</returns>
    public static ExternalLogin Restore(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive,
        Guid userId,
        ExternalLoginProvider provider,
        string providerUserId,
        string email,
        bool emailVerified,
        DateTime linkedAtUtc,
        DateTime? lastUsedAtUtc)
    {
        return new ExternalLogin(
            id,
            createdAt,
            updateAt,
            isActive,
            userId,
            provider,
            providerUserId,
            email,
            emailVerified,
            linkedAtUtc,
            lastUsedAtUtc);
    }

    /// <summary>
    /// Operação para registrar uso do login externo.
    /// </summary>
    /// <param name="usedAtUtc">Data de uso em UTC.</param>
    public void RegisterUsage(DateTime usedAtUtc)
    {
        DomainException.When(usedAtUtc == default, "A data de uso do login externo é obrigatória.");
        DomainException.When(usedAtUtc < LinkedAtUtc, "A data de uso do login externo não pode ser anterior ao vínculo.");
        DomainException.When(LastUsedAtUtc.HasValue && usedAtUtc < LastUsedAtUtc.Value, "A data de uso do login externo não pode ser anterior ao último uso.");

        LastUsedAtUtc = usedAtUtc;
        SetUpdateData();
    }


    /// <summary>
    /// Operação para validar a consistência do login externo.
    /// </summary>
    private void Validate()
    {
        DomainException.When(Id == Guid.Empty, "O identificador do login externo é obrigatório.");
        DomainException.When(UserId == Guid.Empty, "O identificador do usuário é obrigatório para vincular login externo.");
        DomainException.When(!Enum.IsDefined(typeof(ExternalLoginProvider), Provider), "Provedor externo de login inválido.");
        DomainException.When(string.IsNullOrWhiteSpace(ProviderUserId), "O identificador externo do provedor é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(Email), "O e-mail externo é obrigatório.");
        DomainException.When(LinkedAtUtc == default, "A data de vínculo do login externo é obrigatória.");

        if (LastUsedAtUtc.HasValue)
            DomainException.When(LastUsedAtUtc.Value < LinkedAtUtc, "O último uso do login externo não pode ser anterior ao vínculo.");
    }

    /// <summary>
    /// Operação para normalizar texto obrigatório.
    /// </summary>
    /// <param name="value">Valor informado.</param>
    /// <returns>Valor normalizado.</returns>
    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
