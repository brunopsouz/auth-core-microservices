using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports.Aggregates;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.Users.UseCases.ChangePassword;

/// <summary>
/// Representa caso de uso para alterar a senha do usuário autenticado.
/// </summary>
internal sealed class ChangePasswordUseCase : IChangePasswordUseCase
{
    private const string PASSWORD_CHANGED_REASON = "password-changed";

    private readonly IPasswordEncripter _passwordEncripter;
    private readonly IPasswordRepository _passwordRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    /// <param name="passwordRepository">Repositório de senha.</param>
    /// <param name="refreshTokenRepository">Repositório de refresh token.</param>
    /// <param name="passwordEncripter">Serviço de criptografia de senha.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public ChangePasswordUseCase(
        IUserReadRepository userReadRepository,
        IPasswordRepository passwordRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordEncripter passwordEncripter,
        IUnitOfWork unitOfWork)
    {
        _userReadRepository = userReadRepository;
        _passwordRepository = passwordRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordEncripter = passwordEncripter;
        _unitOfWork = unitOfWork;
    }

    #endregion

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

        var newPasswordHash = _passwordEncripter.Encrypt(command.NewPassword);
        var updatedPassword = password.Change(newPasswordHash, PasswordStatus.Active);

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _passwordRepository.UpdateAsync(updatedPassword);
            await _refreshTokenRepository.RevokeActiveByUserIdAsync(user.Id, DateTime.UtcNow, PASSWORD_CHANGED_REASON);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
