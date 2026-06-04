using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.UseCases.Authentication.LoginSession;

/// <summary>
/// Representa caso de uso para autenticar um usuario por sessao.
/// </summary>
internal sealed class LoginSessionUseCase : ILoginSessionUseCase
{
    private const string INVALID_CREDENTIALS_MESSAGE = "As credenciais informadas sao invalidas.";

    /// <summary>
    /// Campo que armazena access token generator.
    /// </summary>
    private readonly IAccessTokenGenerator _accessTokenGenerator;
    /// <summary>
    /// Campo que armazena durable session repository.
    /// </summary>
    private readonly IDurableSessionRepository _durableSessionRepository;
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
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;
    /// <summary>
    /// Campo que armazena user read repository.
    /// </summary>
    private readonly IUserReadRepository _userReadRepository;


    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="userReadRepository">Repositorio de leitura de usuario.</param>
    /// <param name="passwordRepository">Repositorio de senha.</param>
    /// <param name="passwordEncripter">Servico de criptografia de senha.</param>
    /// <param name="durableSessionRepository">Repositorio duravel da sessao autenticada.</param>
    /// <param name="sessionStore">Store de sessao autenticada.</param>
    /// <param name="sessionService">Servico de calculo de expiracao da sessao.</param>
    /// <param name="accessTokenGenerator">Gerador do access token curto.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public LoginSessionUseCase(
        IUserReadRepository userReadRepository,
        IPasswordRepository passwordRepository,
        IPasswordEncripter passwordEncripter,
        IDurableSessionRepository durableSessionRepository,
        ISessionStore sessionStore,
        ISessionService sessionService,
        IAccessTokenGenerator accessTokenGenerator,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(userReadRepository);
        ArgumentNullException.ThrowIfNull(passwordRepository);
        ArgumentNullException.ThrowIfNull(passwordEncripter);
        ArgumentNullException.ThrowIfNull(durableSessionRepository);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(accessTokenGenerator);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _userReadRepository = userReadRepository;
        _passwordRepository = passwordRepository;
        _passwordEncripter = passwordEncripter;
        _durableSessionRepository = durableSessionRepository;
        _sessionStore = sessionStore;
        _sessionService = sessionService;
        _accessTokenGenerator = accessTokenGenerator;
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Operacao para autenticar um usuario por sessao.
    /// </summary>
    /// <param name="command">Comando com as credenciais e metadados da sessao.</param>
    /// <returns>Resultado da autenticacao por sessao.</returns>
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
    /// Operacao para concluir a autenticacao por sessao.
    /// </summary>
    /// <param name="user">Usuario autenticado.</param>
    /// <param name="password">Senha valida do usuario.</param>
    /// <param name="command">Comando com os metadados da sessao.</param>
    /// <returns>Resultado da autenticacao por sessao.</returns>
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
            user.SecurityStamp,
            _sessionService.GetExpiresAtUtc(),
            command.IpAddress,
            command.UserAgent);
        var accessToken = _accessTokenGenerator.Generate(user, session);

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            if (updatedPassword is not null)
                await _passwordRepository.UpdateAsync(updatedPassword);

            await _durableSessionRepository.AddAsync(session);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        await _sessionStore.SaveAsync(session);

        return new AuthenticatedUserSessionResult
        {
            SessionId = session.SessionId,
            AccessToken = accessToken.Token,
            AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
            UserIdentifier = user.UserIdentifier,
            Email = user.Email.Value,
            ExpiresAtUtc = session.ExpiresAtUtc
        };
    }

    /// <summary>
    /// Operacao para registrar uma falha de autenticacao na senha persistida.
    /// </summary>
    /// <param name="password">Senha a ser atualizada.</param>
    private async Task RegisterLoginFailureAsync(Password password)
    {
        var updatedPassword = password.RegisterLoginFailure();
        await _passwordRepository.UpdateAsync(updatedPassword);
    }

    /// <summary>
    /// Operacao para verificar se a senha pode ser usada na autenticacao.
    /// </summary>
    /// <param name="password">Senha do usuario.</param>
    /// <returns><c>true</c> quando a senha pode autenticar; caso contrario, <c>false</c>.</returns>
    private static bool CanAuthenticate(Password password)
    {
        return password.Status is PasswordStatus.Active or PasswordStatus.FirstAccess
            && !password.IsLocked();
    }

    /// <summary>
    /// Operacao para indicar se o login bem-sucedido precisa limpar o historico de falhas.
    /// </summary>
    /// <param name="password">Senha autenticada.</param>
    /// <returns><c>true</c> quando as tentativas devem ser resetadas; caso contrario, <c>false</c>.</returns>
    private static bool ShouldResetLoginAttempts(Password password)
    {
        return password.LoginAttempt.FailedAttempts > 0 || password.IsLocked();
    }

    /// <summary>
    /// Operacao para obter o status que a senha deve manter apos autenticacao valida.
    /// </summary>
    /// <param name="password">Senha autenticada.</param>
    /// <returns>Status consistente apos reset das tentativas.</returns>
    private static PasswordStatus GetAuthenticatedPasswordStatus(Password password)
    {
        return password.Status == PasswordStatus.FirstAccess
            ? PasswordStatus.FirstAccess
            : PasswordStatus.Active;
    }

    /// <summary>
    /// Operacao para normalizar o e-mail informado.
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
    /// Operacao para criar a falha generica de autenticacao.
    /// </summary>
    /// <returns>Excecao de acesso nao autorizado.</returns>
    private static UnauthorizedAccessException CreateInvalidCredentialsException()
    {
        return new UnauthorizedAccessException(INVALID_CREDENTIALS_MESSAGE);
    }

    /// <summary>
    /// Operacao para criar a falha de autenticacao por estado do usuario.
    /// </summary>
    /// <param name="user">Usuario alvo da autenticacao.</param>
    /// <returns>Excecao de acesso proibido.</returns>
    private static ForbiddenException CreateCannotSignInException(User user)
    {
        return user.Status switch
        {
            UserStatus.PendingEmailVerification => new ForbiddenException("O usuario precisa verificar o e-mail antes de autenticar."),
            UserStatus.Blocked => new ForbiddenException("O usuario esta bloqueado para autenticacao."),
            _ => new ForbiddenException("O usuario nao pode autenticar no momento.")
        };
    }
}
