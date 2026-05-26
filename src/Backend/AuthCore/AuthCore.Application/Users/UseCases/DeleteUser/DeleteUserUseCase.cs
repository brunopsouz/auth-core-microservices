using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.Users.UseCases.DeleteUser;

/// <summary>
/// Representa caso de uso para excluir o usuário autenticado.
/// </summary>
internal sealed class DeleteUserUseCase : IDeleteUserUseCase
{
    private const string USER_DEACTIVATED_REASON = "user-deactivated";

    /// <summary>
    /// Campo que armazena password repository.
    /// </summary>
    private readonly IPasswordRepository _passwordRepository;
    /// <summary>
    /// Campo que armazena refresh token repository.
    /// </summary>
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;
    /// <summary>
    /// Campo que armazena user read repository.
    /// </summary>
    private readonly IUserReadRepository _userReadRepository;
    /// <summary>
    /// Campo que armazena user repository.
    /// </summary>
    private readonly IUserRepository _userRepository;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    /// <param name="userRepository">Repositório de escrita de usuário.</param>
    /// <param name="passwordRepository">Repositório de senha.</param>
    /// <param name="refreshTokenRepository">Repositório de refresh token.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public DeleteUserUseCase(
        IUserReadRepository userReadRepository,
        IUserRepository userRepository,
        IPasswordRepository passwordRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _userReadRepository = userReadRepository;
        _userRepository = userRepository;
        _passwordRepository = passwordRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Operação para excluir o usuário autenticado.
    /// </summary>
    /// <param name="command">Comando da exclusão do usuário autenticado.</param>
    public async Task Execute(DeleteUserCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await _userReadRepository.GetByUserIdentifierAsync(command.UserIdentifier);

        if (user is null || !user.IsActive)
            throw new NotFoundException("Usuário não encontrado.");

        var password = await _passwordRepository.GetByUserIdAsync(user.Id);

        user.Deactivate();

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _userRepository.UpdateAsync(user);

            if (password is not null)
                await _passwordRepository.UpdateAsync(password.MarkAsDeactivated());

            await _refreshTokenRepository.RevokeActiveByUserIdAsync(user.Id, DateTime.UtcNow, USER_DEACTIVATED_REASON);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
