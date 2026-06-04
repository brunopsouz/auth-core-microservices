using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.UseCases.Users.ChangePassword;

/// <summary>
/// Representa caso de uso para alterar a senha do usuário autenticado.
/// </summary>
internal sealed class ChangePasswordUseCase : IChangePasswordUseCase
{
    private const string PASSWORD_CHANGED_REASON = "password-changed";
    private const string PARTIAL_CACHE_INVALIDATION_MESSAGE = "A senha foi alterada e as sessoes persistidas foram revogadas, mas a invalidacao do cache de sessao falhou.";

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
    /// Campo que armazena refresh token repository.
    /// </summary>
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    /// <summary>
    /// Campo que armazena session store.
    /// </summary>
    private readonly ISessionStore _sessionStore;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;
    /// <summary>
    /// Campo que armazena user repository.
    /// </summary>
    private readonly IUserRepository _userRepository;
    /// <summary>
    /// Campo que armazena user read repository.
    /// </summary>
    private readonly IUserReadRepository _userReadRepository;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userRepository">Repositório de escrita de usuário.</param>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    /// <param name="passwordRepository">Repositório de senha.</param>
    /// <param name="durableSessionRepository">Repositório durável de sessão.</param>
    /// <param name="sessionStore">Store de sessão autenticada.</param>
    /// <param name="refreshTokenRepository">Repositório de refresh token.</param>
    /// <param name="passwordEncripter">Serviço de criptografia de senha.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public ChangePasswordUseCase(
        IUserRepository userRepository,
        IUserReadRepository userReadRepository,
        IPasswordRepository passwordRepository,
        IDurableSessionRepository durableSessionRepository,
        ISessionStore sessionStore,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordEncripter passwordEncripter,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(userReadRepository);
        ArgumentNullException.ThrowIfNull(passwordRepository);
        ArgumentNullException.ThrowIfNull(durableSessionRepository);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(refreshTokenRepository);
        ArgumentNullException.ThrowIfNull(passwordEncripter);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _userRepository = userRepository;
        _userReadRepository = userReadRepository;
        _passwordRepository = passwordRepository;
        _durableSessionRepository = durableSessionRepository;
        _sessionStore = sessionStore;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordEncripter = passwordEncripter;
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Operação para alterar a senha do usuário autenticado.
    /// </summary>
    /// <param name="command">Comando da alteração de senha.</param>
    public async Task Execute(ChangePasswordCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await _userReadRepository.GetByUserIdentifierAsync(command.UserIdentifier);

        if (user is null || !user.IsActive)
            throw new NotFoundException("Usuário não encontrado.");

        var password = await _passwordRepository.GetByUserIdAsync(user.Id);

        if (password is null)
            throw new NotFoundException("Senha do usuário não encontrada.");

        if (!_passwordEncripter.IsValid(command.CurrentPassword, password.Value))
            throw new DomainException("A senha atual informada é inválida.");

        Password.ValidateWithConfirmation(command.NewPassword, command.ConfirmNewPassword);

        var changedAtUtc = DateTime.UtcNow;
        var newPasswordHash = _passwordEncripter.Encrypt(command.NewPassword);
        var updatedPassword = password.Change(newPasswordHash, PasswordStatus.Active);
        user.RotateSecurityStamp();

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _passwordRepository.UpdateAsync(updatedPassword);
            await _userRepository.UpdateAsync(user);
            await _durableSessionRepository.RevokeActiveByUserIdAsync(
                user.Id,
                SessionRevocationReason.PasswordChanged,
                changedAtUtc);
            await _refreshTokenRepository.RevokeActiveByUserIdAsync(user.Id, changedAtUtc, PASSWORD_CHANGED_REASON);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        try
        {
            await _sessionStore.RevokeAllAsync(user.Id);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(PARTIAL_CACHE_INVALIDATION_MESSAGE, exception);
        }
    }
}
