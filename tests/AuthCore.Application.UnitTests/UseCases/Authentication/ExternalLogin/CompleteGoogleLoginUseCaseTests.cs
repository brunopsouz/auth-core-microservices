using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using DomainExternalLogin = AuthCore.Domain.Users.ExternalLogin;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.ExternalLogin;

public sealed class CompleteGoogleLoginUseCaseTests
{
    [Fact]
    public async Task Execute_WhenExternalLoginExists_ShouldAuthenticateLinkedUser()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-123",
            user.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(externalLogin);

        var result = await context.UseCase.Execute(CreateCommand(
            providerUserId: "google-sub-123",
            email: user.Email.Value));

        Assert.False(result.RequiresOnboarding);
        Assert.Equal(context.ReturnUrlValidator.Result, result.RedirectUrl);
        Assert.NotNull(result.Session);
        Assert.Equal(user.UserIdentifier, result.Session.UserIdentifier);
        Assert.Equal(user.Email.Value, result.Session.Email);
        Assert.Equal(context.AccessTokenGenerator.Result.Token, result.Session.AccessToken);

        var updatedExternalLogin = Assert.Single(context.ExternalLoginRepository!.UpdatedExternalLogins);
        Assert.Equal(externalLogin.Id, updatedExternalLogin.Id);
        Assert.NotNull(updatedExternalLogin.LastUsedAtUtc);
        Assert.Empty(context.ExternalLoginRepository.AddedExternalLogins);
        Assert.Empty(context.UserRepository.UpdatedUsers);

        var savedDurableSession = Assert.Single(context.DurableSessionRepository.AddedSessions);
        var savedCachedSession = Assert.Single(context.SessionStore.SavedSessions);

        Assert.Equal(user.Id, savedDurableSession.UserId);
        Assert.Equal(savedDurableSession.SessionId, savedCachedSession.SessionId);
        Assert.Equal(savedDurableSession.SessionId, context.AccessTokenGenerator.LastGeneratedSession!.SessionId);
        Assert.Equal(1, context.UnitOfWork.BegunTransactions);
        Assert.Equal(1, context.UnitOfWork.CommittedTransactions);
        Assert.Equal(0, context.UnitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenVerifiedEmailMatchesExistingUser_ShouldLinkAndAuthenticate()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateUnverifiedUser();

        context.UserReadRepository.Store(user);

        var result = await context.UseCase.Execute(CreateCommand(
            providerUserId: "google-sub-456",
            email: user.Email.Value));

        Assert.False(result.RequiresOnboarding);
        Assert.Equal(context.ReturnUrlValidator.Result, result.RedirectUrl);
        Assert.NotNull(result.Session);
        Assert.Equal(user.UserIdentifier, result.Session.UserIdentifier);
        Assert.True(user.IsEmailVerified);
        Assert.True(user.CanSignIn);

        var linkedExternalLogin = Assert.Single(context.ExternalLoginRepository!.AddedExternalLogins);
        Assert.Equal(user.Id, linkedExternalLogin.UserId);
        Assert.Equal(ExternalLoginProvider.Google, linkedExternalLogin.Provider);
        Assert.Equal("google-sub-456", linkedExternalLogin.ProviderUserId);
        Assert.Equal(user.Email.Value, linkedExternalLogin.Email);
        Assert.True(linkedExternalLogin.EmailVerified);

        var updatedUser = Assert.Single(context.UserRepository.UpdatedUsers);
        Assert.Equal(user.Id, updatedUser.Id);
        Assert.Single(context.DurableSessionRepository.AddedSessions);
        Assert.Single(context.SessionStore.SavedSessions);
        Assert.Equal(1, context.UnitOfWork.BegunTransactions);
        Assert.Equal(1, context.UnitOfWork.CommittedTransactions);
        Assert.Equal(0, context.UnitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenEmailIsNotVerified_ShouldRejectAutomaticLink()
    {
        var context = CreateContext();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.UseCase.Execute(CreateCommand(
            providerUserId: "google-sub-789",
            email: "new.user@authcore.dev",
            emailVerified: false)));

        Assert.Equal("O Google nao confirmou o e-mail informado.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository!.AddedExternalLogins);
        Assert.Empty(context.DurableSessionRepository.AddedSessions);
        Assert.Empty(context.SessionStore.SavedSessions);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenExternalLoginExistsAndEmailIsNotVerified_ShouldRejectAuthentication()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-unverified",
            user.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(externalLogin);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.UseCase.Execute(CreateCommand(
            providerUserId: "google-sub-unverified",
            email: user.Email.Value,
            emailVerified: false)));

        Assert.Equal("O Google nao confirmou o e-mail informado.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository!.UpdatedExternalLogins);
        Assert.Empty(context.DurableSessionRepository.AddedSessions);
        Assert.Empty(context.SessionStore.SavedSessions);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenUserIsBlocked_ShouldThrowForbiddenException()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateBlockedUser();
        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-blocked",
            user.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(externalLogin);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => context.UseCase.Execute(CreateCommand(
            providerUserId: "google-sub-blocked",
            email: user.Email.Value)));

        Assert.Equal("O usuario esta bloqueado para autenticacao.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository!.UpdatedExternalLogins);
        Assert.Empty(context.DurableSessionRepository.AddedSessions);
        Assert.Empty(context.SessionStore.SavedSessions);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenPersistenceFails_ShouldRollbackTransaction()
    {
        var context = CreateContext(externalLoginRepository: new ThrowingExternalLoginRepository());
        var user = AuthenticationFixtures.CreateVerifiedUser();

        context.UserReadRepository.Store(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.UseCase.Execute(CreateCommand(
            providerUserId: "google-sub-failure",
            email: user.Email.Value)));

        Assert.Equal(1, context.UnitOfWork.BegunTransactions);
        Assert.Equal(0, context.UnitOfWork.CommittedTransactions);
        Assert.Equal(1, context.UnitOfWork.RolledBackTransactions);
        Assert.Empty(context.DurableSessionRepository.AddedSessions);
        Assert.Empty(context.SessionStore.SavedSessions);
    }

    [Fact]
    public async Task Execute_WhenVerifiedEmailDoesNotMatchUser_ShouldRequireOnboarding()
    {
        var context = CreateContext();

        var result = await context.UseCase.Execute(CreateCommand(
            providerUserId: "google-sub-new-user",
            email: "new.user@authcore.dev"));

        Assert.True(result.RequiresOnboarding);
        Assert.Equal("/onboarding?provider=google", result.RedirectUrl);
        Assert.Null(result.Session);
        Assert.Empty(context.ExternalLoginRepository!.AddedExternalLogins);
        Assert.Empty(context.DurableSessionRepository.AddedSessions);
        Assert.Empty(context.SessionStore.SavedSessions);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenCancellationIsRequested_ShouldStopBeforePersistence()
    {
        var context = CreateContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.UseCase.Execute(
            CreateCommand(
                providerUserId: "google-sub-cancelled",
                email: "cancelled@authcore.dev"),
            cancellationTokenSource.Token));

        Assert.Empty(context.ExternalLoginRepository!.AddedExternalLogins);
        Assert.Empty(context.DurableSessionRepository.AddedSessions);
        Assert.Empty(context.SessionStore.SavedSessions);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenCommitIsCancelled_ShouldRollbackAndSkipCache()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var unitOfWork = new SpyUnitOfWork
        {
            CommitException = new OperationCanceledException(cancellationTokenSource.Token)
        };
        var context = CreateContext(unitOfWork: unitOfWork);
        var user = AuthenticationFixtures.CreateVerifiedUser();
        context.UserReadRepository.Store(user);

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.UseCase.Execute(
            CreateCommand(
                providerUserId: "google-sub-commit-cancelled",
                email: user.Email.Value),
            cancellationTokenSource.Token));

        Assert.Equal(1, context.UnitOfWork.BegunTransactions);
        Assert.Equal(1, context.UnitOfWork.CommittedTransactions);
        Assert.Equal(1, context.UnitOfWork.RolledBackTransactions);
        Assert.Empty(context.SessionStore.SavedSessions);
    }

    private static CompleteGoogleLoginCommand CreateCommand(
        string providerUserId,
        string email,
        bool emailVerified = true)
    {
        return new CompleteGoogleLoginCommand
        {
            ProviderUserId = providerUserId,
            Email = email,
            EmailVerified = emailVerified,
            FullName = "Google User",
            PictureUrl = "https://lh3.googleusercontent.com/avatar",
            ReturnUrl = "https://app.authcore.dev/home",
            IpAddress = "127.0.0.1",
            UserAgent = "Mozilla/5.0"
        };
    }

    private static TestContext CreateContext(
        IExternalLoginRepository? externalLoginRepository = null,
        SpyUnitOfWork? unitOfWork = null)
    {
        var externalLoginReadRepository = new FakeExternalLoginReadRepository();
        var resolvedExternalLoginRepository = externalLoginRepository ?? new FakeExternalLoginRepository();
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var durableSessionRepository = new FakeDurableSessionRepository();
        var sessionStore = new FakeSessionStore();
        var sessionService = new FakeSessionService
        {
            ExpiresAtUtc = DateTime.UtcNow.AddHours(8)
        };
        var accessTokenGenerator = new FakeAccessTokenGenerator();
        var returnUrlValidator = new FakeExternalReturnUrlValidator();
        var resolvedUnitOfWork = unitOfWork ?? new SpyUnitOfWork();

        var useCase = new CompleteGoogleLoginUseCase(
            externalLoginReadRepository,
            resolvedExternalLoginRepository,
            userReadRepository,
            userRepository,
            durableSessionRepository,
            sessionStore,
            sessionService,
            accessTokenGenerator,
            returnUrlValidator,
            resolvedUnitOfWork);

        return new TestContext(
            useCase,
            externalLoginReadRepository,
            resolvedExternalLoginRepository,
            userReadRepository,
            userRepository,
            durableSessionRepository,
            sessionStore,
            accessTokenGenerator,
            returnUrlValidator,
            resolvedUnitOfWork);
    }

    private sealed class TestContext
    {
        public TestContext(
            CompleteGoogleLoginUseCase useCase,
            FakeExternalLoginReadRepository externalLoginReadRepository,
            IExternalLoginRepository externalLoginRepository,
            FakeUserReadRepository userReadRepository,
            FakeUserRepository userRepository,
            FakeDurableSessionRepository durableSessionRepository,
            FakeSessionStore sessionStore,
            FakeAccessTokenGenerator accessTokenGenerator,
            FakeExternalReturnUrlValidator returnUrlValidator,
            SpyUnitOfWork unitOfWork)
        {
            UseCase = useCase;
            ExternalLoginReadRepository = externalLoginReadRepository;
            ExternalLoginRepository = externalLoginRepository as FakeExternalLoginRepository;
            UserReadRepository = userReadRepository;
            UserRepository = userRepository;
            DurableSessionRepository = durableSessionRepository;
            SessionStore = sessionStore;
            AccessTokenGenerator = accessTokenGenerator;
            ReturnUrlValidator = returnUrlValidator;
            UnitOfWork = unitOfWork;
        }

        public CompleteGoogleLoginUseCase UseCase { get; }

        public FakeExternalLoginReadRepository ExternalLoginReadRepository { get; }

        public FakeExternalLoginRepository? ExternalLoginRepository { get; }

        public FakeUserReadRepository UserReadRepository { get; }

        public FakeUserRepository UserRepository { get; }

        public FakeDurableSessionRepository DurableSessionRepository { get; }

        public FakeSessionStore SessionStore { get; }

        public FakeAccessTokenGenerator AccessTokenGenerator { get; }

        public FakeExternalReturnUrlValidator ReturnUrlValidator { get; }

        public SpyUnitOfWork UnitOfWork { get; }
    }
}
