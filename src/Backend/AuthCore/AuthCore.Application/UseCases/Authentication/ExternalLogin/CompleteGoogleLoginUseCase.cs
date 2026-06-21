using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using DomainExternalLogin = AuthCore.Domain.Users.ExternalLogin;

namespace AuthCore.Application.UseCases.Authentication.ExternalLogin;

/// <summary>
/// Representa caso de uso para concluir login com Google.
/// </summary>
internal sealed class CompleteGoogleLoginUseCase : ICompleteGoogleLoginUseCase
{
    private const string OnboardingRedirectUrl = "/onboarding?provider=google";

    /// <summary>
    /// Campo que armazena access token generator.
    /// </summary>
    private readonly IAccessTokenGenerator _accessTokenGenerator;

    /// <summary>
    /// Campo que armazena durable session repository.
    /// </summary>
    private readonly IDurableSessionRepository _durableSessionRepository;

    /// <summary>
    /// Campo que armazena external login read repository.
    /// </summary>
    private readonly IExternalLoginReadRepository _externalLoginReadRepository;

    /// <summary>
    /// Campo que armazena external login repository.
    /// </summary>
    private readonly IExternalLoginRepository _externalLoginRepository;

    /// <summary>
    /// Campo que armazena return url validator.
    /// </summary>
    private readonly IExternalReturnUrlValidator _returnUrlValidator;

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
    /// Campo que armazena user repository.
    /// </summary>
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    /// <param name="externalLoginReadRepository">Repositorio de leitura de login externo.</param>
    /// <param name="externalLoginRepository">Repositorio de escrita de login externo.</param>
    /// <param name="userReadRepository">Repositorio de leitura de usuario.</param>
    /// <param name="userRepository">Repositorio de escrita de usuario.</param>
    /// <param name="durableSessionRepository">Repositorio duravel da sessao autenticada.</param>
    /// <param name="sessionStore">Store de sessao autenticada.</param>
    /// <param name="sessionService">Servico de calculo de expiracao da sessao.</param>
    /// <param name="accessTokenGenerator">Gerador do access token curto.</param>
    /// <param name="returnUrlValidator">Validador de URL de retorno.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public CompleteGoogleLoginUseCase(
        IExternalLoginReadRepository externalLoginReadRepository,
        IExternalLoginRepository externalLoginRepository,
        IUserReadRepository userReadRepository,
        IUserRepository userRepository,
        IDurableSessionRepository durableSessionRepository,
        ISessionStore sessionStore,
        ISessionService sessionService,
        IAccessTokenGenerator accessTokenGenerator,
        IExternalReturnUrlValidator returnUrlValidator,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(externalLoginReadRepository);
        ArgumentNullException.ThrowIfNull(externalLoginRepository);
        ArgumentNullException.ThrowIfNull(userReadRepository);
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(durableSessionRepository);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(accessTokenGenerator);
        ArgumentNullException.ThrowIfNull(returnUrlValidator);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _externalLoginReadRepository = externalLoginReadRepository;
        _externalLoginRepository = externalLoginRepository;
        _userReadRepository = userReadRepository;
        _userRepository = userRepository;
        _durableSessionRepository = durableSessionRepository;
        _sessionStore = sessionStore;
        _sessionService = sessionService;
        _accessTokenGenerator = accessTokenGenerator;
        _returnUrlValidator = returnUrlValidator;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Operacao para concluir login com Google.
    /// </summary>
    /// <param name="command">Comando com dados externos do Google.</param>
    /// <param name="cancellationToken">Token de cancelamento da operacao.</param>
    /// <returns>Resultado da conclusao do login com Google.</returns>
    public async Task<CompleteGoogleLoginResult> Execute(
        CompleteGoogleLoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var redirectUrl = _returnUrlValidator.Validate(command.ReturnUrl);
        var providerUserId = NormalizeRequired(command.ProviderUserId);
        var email = NormalizeEmail(command.Email);

        if (string.IsNullOrWhiteSpace(providerUserId))
            throw new ValidationException("O identificador externo do Google e obrigatorio.");

        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("O e-mail externo e obrigatorio.");

        if (!command.EmailVerified)
            throw new ValidationException("O Google nao confirmou o e-mail informado.");

        var externalLogin = await _externalLoginReadRepository.GetByProviderUserIdAsync(
            ExternalLoginProvider.Google,
            providerUserId,
            cancellationToken);

        if (externalLogin is not null)
        {
            return await AuthenticateLinkedUserAsync(
                externalLogin,
                redirectUrl,
                command,
                cancellationToken);
        }

        var user = await _userReadRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            return new CompleteGoogleLoginResult
            {
                RequiresOnboarding = true,
                RedirectUrl = OnboardingRedirectUrl
            };
        }

        return await LinkAndAuthenticateExistingUserAsync(
            user,
            providerUserId,
            email,
            redirectUrl,
            command,
            cancellationToken);
    }

    /// <summary>
    /// Operacao para autenticar usuario ja vinculado ao Google.
    /// </summary>
    /// <param name="externalLogin">Vinculo externo encontrado.</param>
    /// <param name="redirectUrl">URL segura de retorno.</param>
    /// <param name="command">Comando com metadados da sessao.</param>
    /// <returns>Resultado da conclusao do login com Google.</returns>
    private async Task<CompleteGoogleLoginResult> AuthenticateLinkedUserAsync(
        DomainExternalLogin externalLogin,
        string redirectUrl,
        CompleteGoogleLoginCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _userReadRepository.GetByIdAsync(
            externalLogin.UserId,
            cancellationToken)
            ?? throw new NotFoundException("Usuario vinculado ao login externo nao foi encontrado.");

        EnsureCanSignIn(user);

        externalLogin.RegisterUsage(DateTime.UtcNow);

        return await PersistAndAuthenticateAsync(
            user,
            redirectUrl,
            command,
            cancellationToken,
            updateExternalLogin: externalLogin);
    }

    /// <summary>
    /// Operacao para vincular Google a usuario existente e autenticar.
    /// </summary>
    /// <param name="user">Usuario encontrado por e-mail verificado.</param>
    /// <param name="providerUserId">Identificador externo do Google.</param>
    /// <param name="email">E-mail normalizado.</param>
    /// <param name="redirectUrl">URL segura de retorno.</param>
    /// <param name="command">Comando com metadados da sessao.</param>
    /// <returns>Resultado da conclusao do login com Google.</returns>
    private async Task<CompleteGoogleLoginResult> LinkAndAuthenticateExistingUserAsync(
        User user,
        string providerUserId,
        string email,
        string redirectUrl,
        CompleteGoogleLoginCommand command,
        CancellationToken cancellationToken)
    {
        EnsureCanLinkExternalLogin(user);

        var existingGoogleLogin = await _externalLoginReadRepository.GetByUserIdAndProviderAsync(
            user.Id,
            ExternalLoginProvider.Google,
            cancellationToken);

        if (existingGoogleLogin is not null)
            throw new ConflictException("O usuario ja possui login Google vinculado.");

        User? updatedUser = null;

        if (!user.IsEmailVerified)
        {
            user.VerifyEmail(DateTime.UtcNow);
            updatedUser = user;
        }

        EnsureCanSignIn(user);

        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            providerUserId,
            email,
            emailVerified: true,
            DateTime.UtcNow);

        return await PersistAndAuthenticateAsync(
            user,
            redirectUrl,
            command,
            cancellationToken,
            addExternalLogin: externalLogin,
            updateUser: updatedUser);
    }

    /// <summary>
    /// Operacao para persistir alteracoes e emitir sessao interna.
    /// </summary>
    /// <param name="user">Usuario autenticado.</param>
    /// <param name="redirectUrl">URL segura de retorno.</param>
    /// <param name="command">Comando com metadados da sessao.</param>
    /// <param name="addExternalLogin">Vinculo externo a adicionar.</param>
    /// <param name="updateExternalLogin">Vinculo externo a atualizar.</param>
    /// <param name="updateUser">Usuario a atualizar.</param>
    /// <returns>Resultado da conclusao do login com Google.</returns>
    private async Task<CompleteGoogleLoginResult> PersistAndAuthenticateAsync(
        User user,
        string redirectUrl,
        CompleteGoogleLoginCommand command,
        CancellationToken cancellationToken,
        DomainExternalLogin? addExternalLogin = null,
        DomainExternalLogin? updateExternalLogin = null,
        User? updateUser = null)
    {
        var session = Session.Issue(
            user.Id,
            user.SecurityStamp,
            _sessionService.GetExpiresAtUtc(),
            command.IpAddress,
            command.UserAgent);
        var accessToken = _accessTokenGenerator.Generate(user, session);

        cancellationToken.ThrowIfCancellationRequested();
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            if (updateUser is not null)
                await _userRepository.UpdateAsync(updateUser, cancellationToken);

            if (addExternalLogin is not null)
                await _externalLoginRepository.AddAsync(addExternalLogin, cancellationToken);

            if (updateExternalLogin is not null)
                await _externalLoginRepository.UpdateAsync(updateExternalLogin, cancellationToken);

            await _durableSessionRepository.AddAsync(session, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        // A transacao duravel ja foi confirmada; a projecao em cache deve ser concluida
        // mesmo se a requisicao HTTP for cancelada neste ponto.
        await _sessionStore.SaveAsync(session, CancellationToken.None);

        return new CompleteGoogleLoginResult
        {
            Session = new AuthenticatedUserSessionResult
            {
                SessionId = session.SessionId,
                AccessToken = accessToken.Token,
                AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
                UserIdentifier = user.UserIdentifier,
                Email = user.Email.Value,
                ExpiresAtUtc = session.ExpiresAtUtc
            },
            RequiresOnboarding = false,
            RedirectUrl = redirectUrl
        };
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
    /// Operacao para garantir que usuario pode autenticar.
    /// </summary>
    /// <param name="user">Usuario alvo da autenticacao.</param>
    private static void EnsureCanSignIn(User user)
    {
        if (user.CanSignIn)
            return;

        throw user.Status switch
        {
            UserStatus.PendingEmailVerification => new ForbiddenException("O usuario precisa verificar o e-mail antes de autenticar."),
            UserStatus.Blocked => new ForbiddenException("O usuario esta bloqueado para autenticacao."),
            _ => new ForbiddenException("O usuario nao pode autenticar no momento.")
        };
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
