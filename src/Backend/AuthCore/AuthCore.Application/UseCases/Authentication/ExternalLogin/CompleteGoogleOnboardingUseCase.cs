using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.Models;
using AuthCore.Domain.Common.Enums;
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
/// Representa caso de uso para concluir onboarding de cadastro com Google.
/// </summary>
internal sealed class CompleteGoogleOnboardingUseCase : ICompleteGoogleOnboardingUseCase
{
    private readonly IAccessTokenGenerator _accessTokenGenerator;
    private readonly IDurableSessionRepository _durableSessionRepository;
    private readonly IExternalLoginReadRepository _externalLoginReadRepository;
    private readonly IExternalLoginRepository _externalLoginRepository;
    private readonly ISessionService _sessionService;
    private readonly ISessionStore _sessionStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// Operacao para criar instancia da classe.
    /// </summary>
    public CompleteGoogleOnboardingUseCase(
        IExternalLoginReadRepository externalLoginReadRepository,
        IExternalLoginRepository externalLoginRepository,
        IUserReadRepository userReadRepository,
        IUserRepository userRepository,
        IDurableSessionRepository durableSessionRepository,
        ISessionStore sessionStore,
        ISessionService sessionService,
        IAccessTokenGenerator accessTokenGenerator,
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
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _externalLoginReadRepository = externalLoginReadRepository;
        _externalLoginRepository = externalLoginRepository;
        _userReadRepository = userReadRepository;
        _userRepository = userRepository;
        _durableSessionRepository = durableSessionRepository;
        _sessionStore = sessionStore;
        _sessionService = sessionService;
        _accessTokenGenerator = accessTokenGenerator;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<AuthenticatedUserSessionResult> Execute(
        CompleteGoogleOnboardingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

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
            throw new ConflictException("A conta Google ja esta vinculada a outro usuario.");

        var existingUser = await _userReadRepository.GetByEmailAsync(email, cancellationToken);

        if (existingUser is not null)
            throw new ConflictException("Ja existe um usuario cadastrado com o e-mail informado.");

        var nowUtc = DateTime.UtcNow;
        var user = User.RegisterExternal(
            command.FirstName,
            command.LastName,
            email,
            command.Contact,
            Role.User,
            nowUtc);
        var googleLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            providerUserId,
            email,
            emailVerified: true,
            nowUtc);
        var session = Session.Issue(
            user.Id,
            user.SecurityStamp,
            _sessionService.GetExpiresAtUtc(),
            command.IpAddress,
            command.UserAgent);
        var accessToken = _accessTokenGenerator.Generate(user, session);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _userRepository.AddAsync(user);
            await _externalLoginRepository.AddAsync(googleLogin, cancellationToken);
            await _durableSessionRepository.AddAsync(session, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        await _sessionStore.SaveAsync(session, CancellationToken.None);

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
    /// Operacao para normalizar texto obrigatorio.
    /// </summary>
    private static string NormalizeRequired(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    /// <summary>
    /// Operacao para normalizar e-mail.
    /// </summary>
    private static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }
}
