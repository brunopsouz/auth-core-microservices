using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.UseCases.Authentication.LoginSession;

/// <summary>
/// Representa caso de uso para autenticar um usuário por sessão.
/// </summary>
internal sealed class LoginSessionUseCase : ILoginSessionUseCase
{
    private const string INVALID_CREDENTIALS_MESSAGE = "As credenciais informadas são inválidas.";

    /// <summary>
    /// Campo que armazena password encripter.
    /// </summary>
    private readonly IPasswordEncripter _passwordEncripter;
    /// <summary>
    /// Campo que armazena password repository.
    /// </summary>
    private readonly IPasswordRepository _passwordRepository;
    /// <summary>
    /// Campo que armazena session service.
    /// </summary>
    private readonly ISessionService _sessionService;
    /// <summary>
    /// Campo que armazena session store.
    /// </summary>
    private readonly ISessionStore _sessionStore;
    /// <summary>
    /// Campo que armazena user read repository.
    /// </summary>
    private readonly IUserReadRepository _userReadRepository;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    /// <param name="passwordRepository">Repositório de senha.</param>
    /// <param name="passwordEncripter">Serviço de criptografia de senha.</param>
    /// <param name="sessionStore">Store de sessão autenticada.</param>
    /// <param name="sessionService">Serviço de cálculo de expiração da sessão.</param>
    public LoginSessionUseCase(
        IUserReadRepository userReadRepository,
        IPasswordRepository passwordRepository,
        IPasswordEncripter passwordEncripter,
        ISessionStore sessionStore,
        ISessionService sessionService)
    {
        _userReadRepository = userReadRepository;
        _passwordRepository = passwordRepository;
        _passwordEncripter = passwordEncripter;
        _sessionStore = sessionStore;
        _sessionService = sessionService;
    }


    /// <summary>
    /// Operação para autenticar um usuário por sessão.
    /// </summary>
    /// <param name="command">Comando com as credenciais e metadados da sessão.</param>
    /// <returns>Resultado da autenticação por sessão.</returns>
    public async Task<AuthenticatedUserSessionResult> Execute(LoginSessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = NormalizeEmail(command.Email);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(command.Password))
            throw CreateInvalidCredentialsException();

        var user = await _userReadRepository.GetByEmailAsync(email);

        if (user is null)
            throw CreateInvalidCredentialsException();

        var password = await _passwordRepository.GetByUserIdAsync(user.Id);

        if (password is null)
            throw CreateInvalidCredentialsException();

        if (!CanAuthenticate(password))
            throw CreateInvalidCredentialsException();

        if (!user.CanSignIn)
            throw CreateCannotSignInException(user);

        if (!_passwordEncripter.IsValid(command.Password, password.Value))
        {
            await RegisterLoginFailureAsync(password);
            throw CreateInvalidCredentialsException();
        }

        return await AuthenticateAsync(user, password, command);
    }


    /// <summary>
    /// Operação para concluir a autenticação por sessão.
    /// </summary>
    /// <param name="user">Usuário autenticado.</param>
    /// <param name="password">Senha válida do usuário.</param>
    /// <param name="command">Comando com os metadados da sessão.</param>
    /// <returns>Resultado da autenticação por sessão.</returns>
    private async Task<AuthenticatedUserSessionResult> AuthenticateAsync(
        User user,
        Password password,
        LoginSessionCommand command)
    {
        var updatedPassword = ShouldResetLoginAttempts(password)
            ? password.ResetLoginAttempts(GetAuthenticatedPasswordStatus(password))
            : null;
        var session = Session.Issue(
            user.Id,
            _sessionService.GetExpiresAtUtc(),
            command.IpAddress,
            command.UserAgent);

        if (updatedPassword is not null)
            await _passwordRepository.UpdateAsync(updatedPassword);

        await _sessionStore.SaveAsync(session);

        return new AuthenticatedUserSessionResult
        {
            SessionId = session.SessionId,
            UserIdentifier = user.UserIdentifier,
            Email = user.Email.Value,
            ExpiresAtUtc = session.ExpiresAtUtc
        };
    }

    /// <summary>
    /// Operação para registrar uma falha de autenticação na senha persistida.
    /// </summary>
    /// <param name="password">Senha a ser atualizada.</param>
    private async Task RegisterLoginFailureAsync(Password password)
    {
        var updatedPassword = password.RegisterLoginFailure();
        await _passwordRepository.UpdateAsync(updatedPassword);
    }

    /// <summary>
    /// Operação para verificar se a senha pode ser usada na autenticação.
    /// </summary>
    /// <param name="password">Senha do usuário.</param>
    /// <returns><c>true</c> quando a senha pode autenticar; caso contrário, <c>false</c>.</returns>
    private static bool CanAuthenticate(Password password)
    {
        return password.Status is PasswordStatus.Active or PasswordStatus.FirstAccess
            && !password.IsLocked();
    }

    /// <summary>
    /// Operação para indicar se o login bem-sucedido precisa limpar o histórico de falhas.
    /// </summary>
    /// <param name="password">Senha autenticada.</param>
    /// <returns><c>true</c> quando as tentativas devem ser resetadas; caso contrário, <c>false</c>.</returns>
    private static bool ShouldResetLoginAttempts(Password password)
    {
        return password.LoginAttempt.FailedAttempts > 0 || password.IsLocked();
    }

    /// <summary>
    /// Operação para obter o status que a senha deve manter após autenticação válida.
    /// </summary>
    /// <param name="password">Senha autenticada.</param>
    /// <returns>Status consistente após reset das tentativas.</returns>
    private static PasswordStatus GetAuthenticatedPasswordStatus(Password password)
    {
        return password.Status == PasswordStatus.FirstAccess
            ? PasswordStatus.FirstAccess
            : PasswordStatus.Active;
    }

    /// <summary>
    /// Operação para normalizar o e-mail informado.
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
    /// Operação para criar a falha genérica de autenticação.
    /// </summary>
    /// <returns>Exceção de acesso não autorizado.</returns>
    private static UnauthorizedAccessException CreateInvalidCredentialsException()
    {
        return new UnauthorizedAccessException(INVALID_CREDENTIALS_MESSAGE);
    }

    /// <summary>
    /// Operação para criar a falha de autenticação por estado do usuário.
    /// </summary>
    /// <param name="user">Usuário alvo da autenticação.</param>
    /// <returns>Exceção de acesso proibido.</returns>
    private static ForbiddenException CreateCannotSignInException(User user)
    {
        return user.Status switch
        {
            UserStatus.PendingEmailVerification => new ForbiddenException("O usuário precisa verificar o e-mail antes de autenticar."),
            UserStatus.Blocked => new ForbiddenException("O usuário está bloqueado para autenticação."),
            _ => new ForbiddenException("O usuário não pode autenticar no momento.")
        };
    }

}
