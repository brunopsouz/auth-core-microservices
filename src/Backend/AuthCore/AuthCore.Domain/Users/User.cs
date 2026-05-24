using AuthCore.Domain.Common;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Domain.Users;

/// <summary>
/// Representa uma conta de usuário.
/// </summary>
public sealed class User : AggregateRoot
{
    /// <summary>
    /// Primeiro nome do usuário.
    /// </summary>
    public string FirstName { get; private set; } = null!;

    /// <summary>
    /// Sobrenome do usuário.
    /// </summary>
    public string LastName { get; private set; } = null!;

    /// <summary>
    /// Nome completo do usuário.
    /// </summary>
    public string FullName { get; private set; } = null!;

    /// <summary>
    /// E-mail do usuário.
    /// </summary>
    public Email Email { get; private set; } = null!;

    /// <summary>
    /// Número de telefone ou celular do usuário.
    /// </summary>
    public string Contact { get; private set; } = null!;

    /// <summary>
    /// Identificador adicional do usuário para uso em claims.
    /// </summary>
    public Guid UserIdentifier { get; private set; }

    /// <summary>
    /// Perfil do usuário.
    /// </summary>
    public Role Role { get; private set; }

    /// <summary>
    /// Status funcional do usuário.
    /// </summary>
    public UserStatus Status { get; private set; }

    /// <summary>
    /// Data da verificação do e-mail.
    /// </summary>
    public DateTime? EmailVerifiedAt { get; private set; }

    /// <summary>
    /// Indica se o e-mail já foi verificado.
    /// </summary>
    public bool IsEmailVerified => EmailVerifiedAt.HasValue;

    /// <summary>
    /// Indica se o usuário pode autenticar.
    /// </summary>
    public bool CanSignIn => IsActive && Status == UserStatus.Active && IsEmailVerified;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private User()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="firstName">Primeiro nome do usuário.</param>
    /// <param name="lastName">Sobrenome do usuário.</param>
    /// <param name="fullName">Nome completo do usuário.</param>
    /// <param name="email">E-mail do usuário.</param>
    /// <param name="contact">Número de contato do usuário.</param>
    /// <param name="role">Perfil do usuário.</param>
    /// <param name="userIdentifier">Identificador adicional do usuário.</param>
    /// <param name="emailVerifiedAt">Data da verificação do e-mail.</param>
    private User(
        string firstName,
        string lastName,
        string fullName,
        Email email,
        string contact,
        Role role,
        UserStatus status,
        Guid userIdentifier,
        DateTime? emailVerifiedAt)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        FullName = fullName.Trim();
        Email = email;
        Contact = contact.Trim();
        Role = role;
        Status = status;
        UserIdentifier = userIdentifier;
        EmailVerifiedAt = emailVerifiedAt;

        Validate();
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador interno do usuário.</param>
    /// <param name="createdAt">Data de criação persistida.</param>
    /// <param name="updateAt">Data da última atualização persistida.</param>
    /// <param name="isActive">Status de atividade persistido.</param>
    /// <param name="firstName">Primeiro nome do usuário.</param>
    /// <param name="lastName">Sobrenome do usuário.</param>
    /// <param name="fullName">Nome completo do usuário.</param>
    /// <param name="email">E-mail do usuário.</param>
    /// <param name="contact">Número de contato do usuário.</param>
    /// <param name="role">Perfil do usuário.</param>
    /// <param name="userIdentifier">Identificador adicional do usuário.</param>
    /// <param name="emailVerifiedAt">Data da verificação do e-mail.</param>
    private User(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive,
        string firstName,
        string lastName,
        string fullName,
        Email email,
        string contact,
        Role role,
        UserStatus status,
        Guid userIdentifier,
        DateTime? emailVerifiedAt)
        : base(id, createdAt, updateAt, isActive)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        FullName = fullName.Trim();
        Email = email;
        Contact = contact.Trim();
        Role = role;
        Status = status;
        UserIdentifier = userIdentifier;
        EmailVerifiedAt = emailVerifiedAt;

        Validate();
    }



    /// <summary>
    /// Operação para registrar um novo usuário.
    /// </summary>
    /// <param name="firstName">Primeiro nome do usuário.</param>
    /// <param name="lastName">Sobrenome do usuário.</param>
    /// <param name="email">E-mail do usuário.</param>
    /// <param name="contact">Número de contato do usuário.</param>
    /// <param name="role">Perfil do usuário.</param>
    /// <returns>Instância de <see cref="User"/> registrada.</returns>
    public static User Register(
        string firstName,
        string lastName,
        string email,
        string contact,
        Role role)
    {
        var normalizedFirstName = firstName.Trim();
        var normalizedLastName = lastName.Trim();

        return new User(
            normalizedFirstName,
            normalizedLastName,
            BuildFullName(normalizedFirstName, normalizedLastName),
            Email.Create(email),
            contact,
            role,
            UserStatus.PendingEmailVerification,
            Guid.NewGuid(),
            emailVerifiedAt: null);
    }

    /// <summary>
    /// Operação para criar um novo usuário.
    /// </summary>
    /// <param name="firstName">Primeiro nome do usuário.</param>
    /// <param name="lastName">Sobrenome do usuário.</param>
    /// <param name="fullName">Nome completo do usuário.</param>
    /// <param name="email">E-mail do usuário.</param>
    /// <param name="contact">Número de contato do usuário.</param>
    /// <param name="role">Perfil do usuário.</param>
    /// <returns>Instância de <see cref="User"/> criada.</returns>
    public static User Create(
        string firstName,
        string lastName,
        string fullName,
        string email,
        string contact,
        Role role)
    {
        return new User(
            firstName,
            lastName,
            string.IsNullOrWhiteSpace(fullName) ? BuildFullName(firstName, lastName) : fullName,
            Email.Create(email),
            contact,
            role,
            UserStatus.PendingEmailVerification,
            Guid.NewGuid(),
            emailVerifiedAt: null);
    }

    /// <summary>
    /// Operação para reconstruir um usuário.
    /// </summary>
    /// <param name="firstName">Primeiro nome do usuário.</param>
    /// <param name="lastName">Sobrenome do usuário.</param>
    /// <param name="fullName">Nome completo do usuário.</param>
    /// <param name="email">E-mail do usuário.</param>
    /// <param name="contact">Número de contato do usuário.</param>
    /// <param name="role">Perfil do usuário.</param>
    /// <param name="userIdentifier">Identificador adicional do usuário.</param>
    /// <param name="emailVerifiedAt">Data da verificação do e-mail.</param>
    /// <returns>Instância de <see cref="User"/> reconstruída.</returns>
    public static User Read(
        string firstName,
        string lastName,
        string fullName,
        string email,
        string contact,
        Role role,
        Guid? userIdentifier = null,
        DateTime? emailVerifiedAt = null)
    {
        return new User(
            firstName,
            lastName,
            string.IsNullOrWhiteSpace(fullName) ? BuildFullName(firstName, lastName) : fullName,
            Email.Create(email),
            contact,
            role,
            emailVerifiedAt.HasValue ? UserStatus.Active : UserStatus.PendingEmailVerification,
            userIdentifier ?? Guid.NewGuid(),
            emailVerifiedAt);
    }

    /// <summary>
    /// Operação para reconstruir um usuário persistido.
    /// </summary>
    /// <param name="id">Identificador interno do usuário.</param>
    /// <param name="createdAt">Data de criação persistida.</param>
    /// <param name="updateAt">Data da última atualização persistida.</param>
    /// <param name="isActive">Status de atividade persistido.</param>
    /// <param name="firstName">Primeiro nome do usuário.</param>
    /// <param name="lastName">Sobrenome do usuário.</param>
    /// <param name="fullName">Nome completo do usuário.</param>
    /// <param name="email">E-mail do usuário.</param>
    /// <param name="contact">Número de contato do usuário.</param>
    /// <param name="role">Perfil do usuário.</param>
    /// <param name="userIdentifier">Identificador adicional do usuário.</param>
    /// <param name="emailVerifiedAt">Data da verificação do e-mail.</param>
    /// <returns>Instância de <see cref="User"/> reconstruída.</returns>
    public static User Restore(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive,
        string firstName,
        string lastName,
        string fullName,
        string email,
        string contact,
        Role role,
        UserStatus status,
        Guid userIdentifier,
        DateTime? emailVerifiedAt)
    {
        return new User(
            id,
            createdAt,
            updateAt,
            isActive,
            firstName,
            lastName,
            string.IsNullOrWhiteSpace(fullName) ? BuildFullName(firstName, lastName) : fullName,
            Email.Create(email),
            contact,
            role,
            status,
            userIdentifier,
            emailVerifiedAt);
    }


    /// <summary>
    /// Operação para verificar o e-mail do usuário.
    /// </summary>
    /// <param name="verifiedAt">Data da verificação do e-mail.</param>
    public void VerifyEmail(DateTime verifiedAt)
    {
        DomainException.When(verifiedAt == default, "A data de verificação do e-mail é obrigatória.");

        if (IsEmailVerified)
            return;

        EmailVerifiedAt = verifiedAt;
        Status = UserStatus.Active;
        SetUpdateData();
    }

    /// <summary>
    /// Operação para alterar o e-mail do usuário.
    /// </summary>
    /// <param name="email">Novo e-mail do usuário.</param>
    public void ChangeEmail(string email)
    {
        Email = Email.Create(email);
        EmailVerifiedAt = null;
        Status = UserStatus.PendingEmailVerification;
        SetUpdateData();
    }

    /// <summary>
    /// Operação para bloquear o usuário.
    /// </summary>
    public void Block()
    {
        Status = UserStatus.Blocked;
        SetUpdateData();
    }

    /// <summary>
    /// Operação para ativar o usuário quando o e-mail já estiver verificado.
    /// </summary>
    public void ActivateUser()
    {
        DomainException.When(!IsEmailVerified, "O usuário só pode ser ativado após verificar o e-mail.");

        Status = UserStatus.Active;
        SetUpdateData();
    }

    /// <summary>
    /// Operação para atualizar os dados do perfil do usuário.
    /// </summary>
    /// <param name="firstName">Primeiro nome do usuário.</param>
    /// <param name="lastName">Sobrenome do usuário.</param>
    /// <param name="contact">Número de contato do usuário.</param>
    public void UpdateProfile(
        string firstName,
        string lastName,
        string contact)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        FullName = BuildFullName(firstName, lastName);
        Contact = contact.Trim();

        Validate();
        SetUpdateData();
    }


    /// <summary>
    /// Operação para montar o nome completo do usuário.
    /// </summary>
    /// <param name="firstName">Primeiro nome do usuário.</param>
    /// <param name="lastName">Sobrenome do usuário.</param>
    /// <returns>Nome completo do usuário.</returns>
    private static string BuildFullName(string firstName, string lastName)
    {
        return $"{firstName.Trim()} {lastName.Trim()}";
    }

    /// <summary>
    /// Operação para validar os dados do usuário.
    /// </summary>
    private void Validate()
    {
        DomainException.When(string.IsNullOrWhiteSpace(FirstName), "O nome é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(LastName), "O sobrenome é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(FullName), "O nome completo é obrigatório.");
        DomainException.When(Email is null, "O e-mail é obrigatório.");
        DomainException.When(string.IsNullOrWhiteSpace(Contact), "O contato é obrigatório.");
        DomainException.When(UserIdentifier == Guid.Empty, "O identificador do usuário é obrigatório.");
        DomainException.When(!Enum.IsDefined(typeof(Role), Role), "Perfil de usuário inválido.");
        DomainException.When(!Enum.IsDefined(typeof(UserStatus), Status), "Status do usuário inválido.");
        DomainException.When(Status == UserStatus.Active && !IsEmailVerified, "O usuário ativo deve possuir e-mail verificado.");
    }

}
