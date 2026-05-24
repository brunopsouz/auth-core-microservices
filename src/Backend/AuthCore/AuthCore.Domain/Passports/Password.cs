using System.Text.RegularExpressions;
using AuthCore.Domain.Common;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;

namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa a credencial de autenticação do usuário.
/// </summary>
public sealed class Password : AggregateRoot
{
    private const int MIN_LENGTH = 8;
    private const int MAX_LENGTH = 24;

    /// <summary>
    /// Identificador único do usuário associado à senha.
    /// </summary>
    public Guid UserId => Id;

    /// <summary>
    /// Valor da senha armazenada para o usuário.
    /// </summary>
    public string Value { get; private set; } = null!;

    // /// <summary>
    // /// Chave secreta utilizada para proteger ou validar a senha.
    // /// </summary>
    // public string? SecretKey { get; private set; } = null!;

    /// <summary>
    /// Número de tentativas de acesso realizadas pelo usuário.
    /// </summary>
    public LoginAttempt LoginAttempt { get; private set; } = null!;

    /// <summary>
    /// Status atual da senha (ex.: ativa, bloqueada, expirada).
    /// </summary>
    public PasswordStatus Status { get; private set; }

    /// <summary>
    /// Inicializa uma instância de <see cref="Password"/> com os dados persistidos da senha.
    /// </summary>
    /// <param name="userId">Identificador do usuário associado à senha.</param>
    /// <param name="hashedPassword">Valor da senha já criptografado.</param>
    /// <param name="attempts">Controle de tentativas de login vinculado à senha.</param>
    /// <param name="status">Status atual da senha.</param>
    private Password(
        Guid userId,
        string hashedPassword,
        LoginAttempt attempts,
        PasswordStatus status) : base(userId)
    {
        ValidateState(userId, hashedPassword, attempts, status);
        Value = hashedPassword;
        Status = status;
        LoginAttempt = attempts;
    }


    /// <summary>
    /// Construtor sem parâmetros utilizado por ferramentas de materialização.
    /// </summary>
    private Password() { }



    /// <summary>
    /// Cria a senha com o valor já criptografado.
    /// </summary>
    /// <param name="userId">Identificador do usuário associado à senha.</param>
    /// <param name="hashedPassword">Senha criptografada.</param>
    /// <param name="status">Status atual da senha (ex.: ativa, bloqueada, expirada).</param>
    /// <returns>Instância criada de <see cref="Password"/>.</returns>
    public static Password Create(
        Guid userId,
        string hashedPassword,
        PasswordStatus status)
    {
        return new Password(
            userId,
            hashedPassword,
            attempts: LoginAttempt.Create(),
            status
        );
    }

    /// <summary>
    /// Restaura a senha a partir de um estado previamente persistido.
    /// </summary>
    /// <param name="userId">Identificador do usuário associado à senha.</param>
    /// <param name="hashedPassword">Senha criptografada persistida.</param>
    /// <param name="attempts">Controle de tentativas de login persistido.</param>
    /// <param name="status">Status persistido da senha.</param>
    /// <returns>Instância restaurada de <see cref="Password"/>.</returns>
    public static Password Restore(
        Guid userId,
        string hashedPassword,
        LoginAttempt attempts,
        PasswordStatus status)
    {
        return new Password(userId, hashedPassword, attempts, status);
    }


    /// <summary>
    /// Valida os critérios mínimos da senha.
    /// </summary>
    /// <param name="password">Senha a validar.</param>
    /// <exception cref="DomainException">Lançada quando o formato é inválido.</exception>
    public static void Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new DomainException("A senha não pode estar vazia.");

        if (password.Length < MIN_LENGTH || password.Length > MAX_LENGTH)
            throw new DomainException($"A senha deve conter entre {MIN_LENGTH} e {MAX_LENGTH} caracteres.");

        var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,24}$");

        if (!regex.IsMatch(password))
            throw new DomainException("A senha deve conter letras maiúsculas, minúsculas, números e caracteres especiais.");
    }

    /// <summary>
    /// Valida a senha e confirma se ambas correspondem.
    /// </summary>
    /// <param name="password">Senha informada.</param>
    /// <param name="confirmPassword">Confirmação da senha.</param>
    /// <exception cref="DomainException">Lançada quando os valores não são compatíveis.</exception>
    public static void ValidateWithConfirmation(string password, string confirmPassword)
    {
        Validate(password);

        if (string.IsNullOrWhiteSpace(confirmPassword))
            throw new DomainException("A confirmação de senha não pode estar vazia.");

        if (password != confirmPassword)
            throw new DomainException("As senhas não correspondem.");
    }

    /// <summary>
    /// Registra uma falha de login e atualiza o status da senha quando o bloqueio é atingido.
    /// </summary>
    /// <returns>Nova instância de <see cref="Password"/> com o estado atualizado.</returns>
    public Password RegisterLoginFailure()
    {
        var updatedAttempts = LoginAttempt.RegisterFailure();
        var updatedStatus = updatedAttempts.IsLocked()
            ? PasswordStatus.Blocked
            : Status;

        return new Password(UserId, Value, updatedAttempts, updatedStatus);
    }

    /// <summary>
    /// Reseta as tentativas de login após autenticação bem-sucedida ou desbloqueio.
    /// </summary>
    /// <param name="restoredStatus">Status que a senha deve retomar após o reset das tentativas.</param>
    /// <returns>Nova instância de <see cref="Password"/> com tentativas zeradas.</returns>
    public Password ResetLoginAttempts(PasswordStatus restoredStatus)
    {
        if (restoredStatus == PasswordStatus.Blocked)
            throw new DomainException("O status restaurado não pode ser bloqueado após o reset das tentativas.");

        return new Password(UserId, Value, LoginAttempt.Reset(), restoredStatus);
    }

    /// <summary>
    /// Operação para alterar a senha criptografada.
    /// </summary>
    /// <param name="hashedPassword">Novo valor criptografado da senha.</param>
    /// <param name="status">Novo status da senha.</param>
    /// <returns>Nova instância de <see cref="Password"/> com a senha atualizada.</returns>
    public Password Change(
        string hashedPassword,
        PasswordStatus status)
    {
        return new Password(UserId, hashedPassword, LoginAttempt.Reset(), status);
    }

    /// <summary>
    /// Operação para desativar a senha do usuário.
    /// </summary>
    /// <returns>Nova instância de <see cref="Password"/> desativada.</returns>
    public Password MarkAsDeactivated()
    {
        return new Password(UserId, Value, LoginAttempt.Reset(), PasswordStatus.Deactivated);
    }

    /// <summary>
    /// Indica se a senha está temporariamente bloqueada para autenticação.
    /// </summary>
    /// <returns><c>true</c> quando existe bloqueio ativo; caso contrário, <c>false</c>.</returns>
    public bool IsLocked()
    {
        return LoginAttempt.IsLocked();
    }

    /// <summary>
    /// Obtém a mensagem com o tempo restante de bloqueio, quando existir.
    /// </summary>
    /// <returns>Mensagem de bloqueio ou <c>null</c> quando a senha não está bloqueada.</returns>
    public string? GetLockMessage()
    {
        return LoginAttempt.GetLockMessage();
    }


    /// <summary>
    /// Valida a consistência do estado interno da senha.
    /// </summary>
    /// <param name="userId">Identificador do usuário associado à senha.</param>
    /// <param name="hashedPassword">Valor criptografado da senha.</param>
    /// <param name="attempts">Controle de tentativas de login da senha.</param>
    /// <param name="status">Status atual da senha.</param>
    /// <exception cref="DomainException">Lançada quando a combinação informada representa um estado inválido.</exception>
    private static void ValidateState(
        Guid userId,
        string hashedPassword,
        LoginAttempt attempts,
        PasswordStatus status)
    {
        if (userId == Guid.Empty)
            throw new DomainException("O identificador do usuário é obrigatório.");

        if (string.IsNullOrWhiteSpace(hashedPassword))
            throw new DomainException("A senha criptografada é obrigatória.");

        if (attempts is null)
            throw new DomainException("Os dados de tentativas de login são obrigatórios.");

        if (!Enum.IsDefined(typeof(PasswordStatus), status))
            throw new DomainException("O status da senha é inválido.");

        var isLocked = attempts.IsLocked();

        if (isLocked && status != PasswordStatus.Blocked)
            throw new DomainException("O status da senha deve ser bloqueado quando existe um bloqueio de login ativo.");

        if (!isLocked && status == PasswordStatus.Blocked)
            throw new DomainException("O status bloqueado exige um bloqueio de login ativo.");
    }

}
