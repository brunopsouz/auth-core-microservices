using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using DomainExternalLogin = AuthCore.Domain.Users.ExternalLogin;

namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa caso de uso para vincular login Google ao usuario autenticado.
/// </summary>
internal sealed class LinkGoogleLoginUseCase : ILinkGoogleLoginUseCase
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
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public LinkGoogleLoginUseCase(
        IExternalLoginReadRepository externalLoginReadRepository,
        IExternalLoginRepository externalLoginRepository,
        IUserReadRepository userReadRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(externalLoginReadRepository);
        ArgumentNullException.ThrowIfNull(externalLoginRepository);
        ArgumentNullException.ThrowIfNull(userReadRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _externalLoginReadRepository = externalLoginReadRepository;
        _externalLoginRepository = externalLoginRepository;
        _userReadRepository = userReadRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Operacao para vincular login Google ao usuario autenticado.
    /// </summary>
    /// <param name="command">Comando com usuario autenticado e dados externos do Google.</param>
    public async Task Execute(LinkGoogleLoginCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.UserIdentifier == Guid.Empty)
            throw new ValidationException("O usuario autenticado e obrigatorio.");

        var providerUserId = NormalizeRequired(command.ProviderUserId);
        var email = NormalizeEmail(command.Email);

        if (string.IsNullOrWhiteSpace(providerUserId))
            throw new ValidationException("O identificador externo do Google e obrigatorio.");

        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("O e-mail externo e obrigatorio.");

        if (!command.EmailVerified)
            throw new ValidationException("O Google nao confirmou o e-mail informado.");

        var user = await _userReadRepository.GetByUserIdentifierAsync(command.UserIdentifier);

        if (user is null || !user.IsActive)
            throw new NotFoundException("Usuario nao encontrado.");

        EnsureCanLinkExternalLogin(user);

        var externalLoginByProvider = await _externalLoginReadRepository.GetByProviderUserIdAsync(
            ExternalLoginProvider.Google,
            providerUserId);

        if (externalLoginByProvider is not null)
        {
            if (externalLoginByProvider.UserId == user.Id)
                throw new ConflictException("O usuario ja possui login Google vinculado.");

            throw new ConflictException("A conta Google ja esta vinculada a outro usuario.");
        }

        var externalLoginByUser = await _externalLoginReadRepository.GetByUserIdAndProviderAsync(
            user.Id,
            ExternalLoginProvider.Google);

        if (externalLoginByUser is not null)
            throw new ConflictException("O usuario ja possui login Google vinculado.");

        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            providerUserId,
            email,
            emailVerified: true,
            DateTime.UtcNow);

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _externalLoginRepository.AddAsync(externalLogin);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Operacao para garantir que usuario pode receber vinculo externo.
    /// </summary>
    /// <param name="user">Usuario alvo do vinculo.</param>
    private static void EnsureCanLinkExternalLogin(User user)
    {
        if (!user.IsActive || user.Status == UserStatus.Blocked)
            throw new ForbiddenException("O usuario esta bloqueado para autenticacao.");
    }

    /// <summary>
    /// Operacao para normalizar texto obrigatorio.
    /// </summary>
    /// <param name="value">Valor informado.</param>
    /// <returns>Valor normalizado.</returns>
    private static string NormalizeRequired(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    /// <summary>
    /// Operacao para normalizar e-mail.
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    /// <returns>E-mail normalizado.</returns>
    private static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }
}
