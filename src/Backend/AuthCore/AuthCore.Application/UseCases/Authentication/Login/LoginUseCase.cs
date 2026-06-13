using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Security.Tokens.Models;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.UseCases.Authentication.Login;

/// <summary>
/// Representa caso de uso para autenticar um usuário no modo token.
/// </summary>
internal sealed class LoginUseCase : ILoginUseCase
{
    private const string INVALID_CREDENTIALS_MESSAGE = "As credenciais informadas são inválidas.";

    /// <summary>
    /// Campo que armazena access token generator.
    /// </summary>
    private readonly IAccessTokenGenerator _accessTokenGenerator;
    /// <summary>
    /// Campo que armazena password encripter.
    /// </summary>
    private readonly IPasswordEncripter _passwordEncripter;
    /// <summary>
    /// Campo que armazena password repository.
    /// </summary>
    private readonly IPasswordRepository _passwordRepository;
    /// <summary>
    /// Campo que armazena refresh token repository.
    /// </summary>
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    /// <summary>
    /// Campo que armazena refresh token service.
    /// </summary>
    private readonly IRefreshTokenService _refreshTokenService;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;
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
    /// <param name="accessTokenGenerator">Gerador de access token.</param>
    /// <param name="refreshTokenService">Serviço de refresh token.</param>
    /// <param name="refreshTokenRepository">Repositório de refresh token.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public LoginUseCase(
        IUserReadRepository userReadRepository,
        IPasswordRepository passwordRepository,
        IPasswordEncripter passwordEncripter,
        IAccessTokenGenerator accessTokenGenerator,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _userReadRepository = userReadRepository;
        _passwordRepository = passwordRepository;
        _passwordEncripter = passwordEncripter;
        _accessTokenGenerator = accessTokenGenerator;
        _refreshTokenService = refreshTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Operação para autenticar um usuário no modo token.
    /// </summary>
    /// <param name="command">Comando com as credenciais do login.</param>
    /// <returns>Resultado da sessão autenticada.</returns>
    public async Task<AuthenticatedSessionResult> Execute(LoginCommand command)
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

        return await AuthenticateAsync(user, password);
    }


    /// <summary>
    /// Operação para concluir a autenticação com emissão inicial da sessão.
    /// </summary>
    /// <param name="user">Usuário autenticado.</param>
    /// <param name="password">Senha válida do usuário.</param>
    /// <returns>Resultado da sessão autenticada.</returns>
    private async Task<AuthenticatedSessionResult> AuthenticateAsync(User user, Password password)
    {
        var accessToken = _accessTokenGenerator.Generate(user);
        var refreshTokenMaterial = _refreshTokenService.Create();
        var refreshTokenExpiresAtUtc = _refreshTokenService.GetExpiresAtUtc();
        var refreshToken = RefreshToken.IssueInitial(user.Id, refreshTokenMaterial.Hash, refreshTokenExpiresAtUtc);
        var updatedPassword = ShouldResetLoginAttempts(password)
            ? password.ResetLoginAttempts(GetAuthenticatedPasswordStatus(password))
            : null;

        if (updatedPassword is null)
        {
            await _refreshTokenRepository.AddAsync(refreshToken);
            return CreateAuthenticatedSessionResult(accessToken, refreshTokenMaterial.Token, refreshToken.ExpiresAtUtc);
        }

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _passwordRepository.UpdateAsync(updatedPassword);
            await _refreshTokenRepository.AddAsync(refreshToken);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        return CreateAuthenticatedSessionResult(accessToken, refreshTokenMaterial.Token, refreshToken.ExpiresAtUtc);
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
    /// Operação para normalizar o e-mail informado no login.
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
    private static UnauthorizedException CreateInvalidCredentialsException()
    {
        return new UnauthorizedException(INVALID_CREDENTIALS_MESSAGE);
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

    /// <summary>
    /// Operação para criar o resultado da sessão autenticada.
    /// </summary>
    /// <param name="accessToken">Access token emitido.</param>
    /// <param name="refreshToken">Refresh token em texto puro.</param>
    /// <param name="refreshTokenExpiresAtUtc">Expiração do refresh token.</param>
    /// <returns>Resultado da autenticação.</returns>
    private static AuthenticatedSessionResult CreateAuthenticatedSessionResult(
        AccessTokenResult accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc)
    {
        return new AuthenticatedSessionResult
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc
        };
    }

}
