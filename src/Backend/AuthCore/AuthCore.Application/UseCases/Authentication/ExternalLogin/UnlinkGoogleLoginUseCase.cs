using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using DomainExternalLogin = AuthCore.Domain.Users.ExternalLogin;

namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa caso de uso para desvincular login Google do usuario autenticado.
/// </summary>
internal sealed class UnlinkGoogleLoginUseCase : IUnlinkGoogleLoginUseCase
{
    /// <summary>
    /// Campo que armazena external login read repository.
    /// </summary>
    private readonly IExternalLoginReadRepository _externalLoginReadRepository;

    /// <summary>
    /// Campo que armazena external login repository.
    /// </summary>
    private readonly IExternalLoginRepository _externalLoginRepository;

    /// <summary>
    /// Campo que armazena password repository.
    /// </summary>
    private readonly IPasswordRepository _passwordRepository;

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
    /// <param name="externalLoginReadRepository">Repositorio de leitura de login externo.</param>
    /// <param name="externalLoginRepository">Repositorio de escrita de login externo.</param>
    /// <param name="userReadRepository">Repositorio de leitura de usuario.</param>
    /// <param name="passwordRepository">Repositorio de senha.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public UnlinkGoogleLoginUseCase(
        IExternalLoginReadRepository externalLoginReadRepository,
        IExternalLoginRepository externalLoginRepository,
        IUserReadRepository userReadRepository,
        IPasswordRepository passwordRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(externalLoginReadRepository);
        ArgumentNullException.ThrowIfNull(externalLoginRepository);
        ArgumentNullException.ThrowIfNull(userReadRepository);
        ArgumentNullException.ThrowIfNull(passwordRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _externalLoginReadRepository = externalLoginReadRepository;
        _externalLoginRepository = externalLoginRepository;
        _userReadRepository = userReadRepository;
        _passwordRepository = passwordRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Operacao para desvincular login Google do usuario autenticado.
    /// </summary>
    /// <param name="command">Comando com usuario autenticado.</param>
    public async Task Execute(UnlinkGoogleLoginCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.UserIdentifier == Guid.Empty)
            throw new ValidationException("O usuario autenticado e obrigatorio.");

        var user = await _userReadRepository.GetByUserIdentifierAsync(command.UserIdentifier);

        if (user is null || !user.IsActive)
            throw new NotFoundException("Usuario nao encontrado.");

        var externalLogin = await _externalLoginReadRepository.GetByUserIdAndProviderAsync(
            user.Id,
            ExternalLoginProvider.Google);

        if (externalLogin is null)
            throw new NotFoundException("Login Google nao encontrado para o usuario autenticado.");

        var password = await _passwordRepository.GetByUserIdAsync(user.Id);

        if (password is null || !CanAuthenticateWithPassword(password))
            throw new ValidationException("Nao e possivel desvincular o ultimo metodo de autenticacao do usuario.");

        await DeleteExternalLoginAsync(externalLogin);
    }

    /// <summary>
    /// Operacao para excluir o vinculo externo em transacao.
    /// </summary>
    /// <param name="externalLogin">Login externo a ser removido.</param>
    private async Task DeleteExternalLoginAsync(DomainExternalLogin externalLogin)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _externalLoginRepository.DeleteAsync(externalLogin);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Operacao para verificar se senha local preserva metodo alternativo de autenticacao.
    /// </summary>
    /// <param name="password">Senha local do usuario.</param>
    /// <returns><c>true</c> quando a senha pode autenticar; caso contrario, <c>false</c>.</returns>
    private static bool CanAuthenticateWithPassword(Password password)
    {
        return password.Status is PasswordStatus.Active or PasswordStatus.FirstAccess
            && !password.IsLocked();
    }
}
