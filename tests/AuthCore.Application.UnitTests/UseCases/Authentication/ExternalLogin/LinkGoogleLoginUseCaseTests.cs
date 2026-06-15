using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using DomainExternalLogin = AuthCore.Domain.Users.ExternalLogin;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.ExternalLogin;

public sealed class LinkGoogleLoginUseCaseTests
{
    [Fact]
    public async Task Execute_WhenGoogleAccountIsAlreadyLinkedToAnotherUser_ShouldThrowConflictException()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var anotherUser = AuthenticationFixtures.CreateVerifiedUser();
        var existingExternalLogin = DomainExternalLogin.LinkGoogle(
            anotherUser.Id,
            "google-sub-conflict",
            anotherUser.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(existingExternalLogin);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => context.UseCase.Execute(CreateCommand(
            user.UserIdentifier,
            "google-sub-conflict",
            user.Email.Value)));

        Assert.Equal("A conta Google ja esta vinculada a outro usuario.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository.AddedExternalLogins);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenGoogleAccountIsAvailable_ShouldLinkToAuthenticatedUser()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();

        context.UserReadRepository.Store(user);

        await context.UseCase.Execute(CreateCommand(
            user.UserIdentifier,
            "google-sub-available",
            user.Email.Value.ToUpperInvariant()));

        var externalLogin = Assert.Single(context.ExternalLoginRepository.AddedExternalLogins);

        Assert.Equal(user.Id, externalLogin.UserId);
        Assert.Equal(ExternalLoginProvider.Google, externalLogin.Provider);
        Assert.Equal("google-sub-available", externalLogin.ProviderUserId);
        Assert.Equal(user.Email.Value, externalLogin.Email);
        Assert.True(externalLogin.EmailVerified);
        Assert.Equal(1, context.UnitOfWork.BegunTransactions);
        Assert.Equal(1, context.UnitOfWork.CommittedTransactions);
        Assert.Equal(0, context.UnitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenUserAlreadyHasGoogleLogin_ShouldThrowConflictException()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var existingExternalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-existing",
            user.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(existingExternalLogin);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => context.UseCase.Execute(CreateCommand(
            user.UserIdentifier,
            "google-sub-other",
            user.Email.Value)));

        Assert.Equal("O usuario ja possui login Google vinculado.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository.AddedExternalLogins);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenEmailIsNotVerified_ShouldThrowValidationException()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();

        context.UserReadRepository.Store(user);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.UseCase.Execute(CreateCommand(
            user.UserIdentifier,
            "google-sub-unverified",
            user.Email.Value,
            emailVerified: false)));

        Assert.Equal("O Google nao confirmou o e-mail informado.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository.AddedExternalLogins);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenPersistenceFails_ShouldRollbackTransaction()
    {
        var context = CreateContext(new ThrowingExternalLoginRepository());
        var user = AuthenticationFixtures.CreateVerifiedUser();

        context.UserReadRepository.Store(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.UseCase.Execute(CreateCommand(
            user.UserIdentifier,
            "google-sub-failure",
            user.Email.Value)));

        Assert.Equal(1, context.UnitOfWork.BegunTransactions);
        Assert.Equal(0, context.UnitOfWork.CommittedTransactions);
        Assert.Equal(1, context.UnitOfWork.RolledBackTransactions);
    }

    private static LinkGoogleLoginCommand CreateCommand(
        Guid userIdentifier,
        string providerUserId,
        string email,
        bool emailVerified = true)
    {
        return new LinkGoogleLoginCommand
        {
            UserIdentifier = userIdentifier,
            ProviderUserId = providerUserId,
            Email = email,
            EmailVerified = emailVerified
        };
    }

    private static TestContext CreateContext(IExternalLoginRepository? externalLoginRepository = null)
    {
        var externalLoginReadRepository = new FakeExternalLoginReadRepository();
        var resolvedExternalLoginRepository = externalLoginRepository ?? new FakeExternalLoginRepository();
        var userReadRepository = new FakeUserReadRepository();
        var unitOfWork = new SpyUnitOfWork();

        var useCase = new LinkGoogleLoginUseCase(
            externalLoginReadRepository,
            resolvedExternalLoginRepository,
            userReadRepository,
            unitOfWork);

        return new TestContext(
            useCase,
            externalLoginReadRepository,
            resolvedExternalLoginRepository,
            userReadRepository,
            unitOfWork);
    }

    private sealed class TestContext
    {
        public TestContext(
            LinkGoogleLoginUseCase useCase,
            FakeExternalLoginReadRepository externalLoginReadRepository,
            IExternalLoginRepository externalLoginRepository,
            FakeUserReadRepository userReadRepository,
            SpyUnitOfWork unitOfWork)
        {
            UseCase = useCase;
            ExternalLoginReadRepository = externalLoginReadRepository;
            ExternalLoginRepository = externalLoginRepository as FakeExternalLoginRepository ?? new FakeExternalLoginRepository();
            UserReadRepository = userReadRepository;
            UnitOfWork = unitOfWork;
        }

        public LinkGoogleLoginUseCase UseCase { get; }

        public FakeExternalLoginReadRepository ExternalLoginReadRepository { get; }

        public FakeExternalLoginRepository ExternalLoginRepository { get; }

        public FakeUserReadRepository UserReadRepository { get; }

        public SpyUnitOfWork UnitOfWork { get; }
    }
}
