using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;
using DomainExternalLogin = AuthCore.Domain.Users.ExternalLogin;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.ExternalLogin;

public sealed class UnlinkGoogleLoginUseCaseTests
{
    [Fact]
    public async Task Execute_WhenExternalLoginDoesNotBelongToUser_ShouldThrowNotFoundException()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var anotherUser = AuthenticationFixtures.CreateVerifiedUser();
        var externalLogin = DomainExternalLogin.LinkGoogle(
            anotherUser.Id,
            "google-sub-other-user",
            anotherUser.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(externalLogin);
        context.PasswordRepository.Store(AuthenticationFixtures.CreatePassword(user.Id));

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => context.UseCase.Execute(new UnlinkGoogleLoginCommand
        {
            UserIdentifier = user.UserIdentifier
        }));

        Assert.Equal("Login Google nao encontrado para o usuario autenticado.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository.DeletedExternalLogins);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenUserWouldLoseLastLoginMethod_ShouldThrowValidationException()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-last-method",
            user.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(externalLogin);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.UseCase.Execute(new UnlinkGoogleLoginCommand
        {
            UserIdentifier = user.UserIdentifier
        }));

        Assert.Equal("Nao e possivel desvincular o ultimo metodo de autenticacao do usuario.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository.DeletedExternalLogins);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenUserHasPassword_ShouldUnlinkGoogleLogin()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-unlink",
            user.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(externalLogin);
        context.PasswordRepository.Store(AuthenticationFixtures.CreatePassword(user.Id));

        await context.UseCase.Execute(new UnlinkGoogleLoginCommand
        {
            UserIdentifier = user.UserIdentifier
        });

        var deletedExternalLogin = Assert.Single(context.ExternalLoginRepository.DeletedExternalLogins);

        Assert.Equal(externalLogin.Id, deletedExternalLogin.Id);
        Assert.Equal(1, context.UnitOfWork.BegunTransactions);
        Assert.Equal(1, context.UnitOfWork.CommittedTransactions);
        Assert.Equal(0, context.UnitOfWork.RolledBackTransactions);
    }

    [Fact]
    public async Task Execute_WhenPasswordIsDeactivated_ShouldThrowValidationException()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-deactivated-password",
            user.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(externalLogin);
        context.PasswordRepository.Store(AuthenticationFixtures.CreatePassword(user.Id, PasswordStatus.Deactivated));

        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.UseCase.Execute(new UnlinkGoogleLoginCommand
        {
            UserIdentifier = user.UserIdentifier
        }));

        Assert.Equal("Nao e possivel desvincular o ultimo metodo de autenticacao do usuario.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository.DeletedExternalLogins);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenPasswordIsBlocked_ShouldThrowValidationException()
    {
        var context = CreateContext();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-blocked-password",
            user.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(externalLogin);
        context.PasswordRepository.Store(AuthenticationFixtures.CreatePassword(
            user.Id,
            failedAttempts: 5));

        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.UseCase.Execute(new UnlinkGoogleLoginCommand
        {
            UserIdentifier = user.UserIdentifier
        }));

        Assert.Equal("Nao e possivel desvincular o ultimo metodo de autenticacao do usuario.", exception.Message);
        Assert.Empty(context.ExternalLoginRepository.DeletedExternalLogins);
        Assert.Equal(0, context.UnitOfWork.BegunTransactions);
    }

    [Fact]
    public async Task Execute_WhenPersistenceFails_ShouldRollbackTransaction()
    {
        var context = CreateContext(new ThrowingExternalLoginRepository());
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var externalLogin = DomainExternalLogin.LinkGoogle(
            user.Id,
            "google-sub-delete-failure",
            user.Email.Value,
            emailVerified: true,
            new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc));

        context.UserReadRepository.Store(user);
        context.ExternalLoginReadRepository.Store(externalLogin);
        context.PasswordRepository.Store(AuthenticationFixtures.CreatePassword(user.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.UseCase.Execute(new UnlinkGoogleLoginCommand
        {
            UserIdentifier = user.UserIdentifier
        }));

        Assert.Equal(1, context.UnitOfWork.BegunTransactions);
        Assert.Equal(0, context.UnitOfWork.CommittedTransactions);
        Assert.Equal(1, context.UnitOfWork.RolledBackTransactions);
    }

    private static TestContext CreateContext(IExternalLoginRepository? externalLoginRepository = null)
    {
        var externalLoginReadRepository = new FakeExternalLoginReadRepository();
        var resolvedExternalLoginRepository = externalLoginRepository ?? new FakeExternalLoginRepository();
        var userReadRepository = new FakeUserReadRepository();
        var passwordRepository = new FakePasswordRepository();
        var unitOfWork = new SpyUnitOfWork();

        var useCase = new UnlinkGoogleLoginUseCase(
            externalLoginReadRepository,
            resolvedExternalLoginRepository,
            userReadRepository,
            passwordRepository,
            unitOfWork);

        return new TestContext(
            useCase,
            externalLoginReadRepository,
            resolvedExternalLoginRepository,
            userReadRepository,
            passwordRepository,
            unitOfWork);
    }

    private sealed class TestContext
    {
        public TestContext(
            UnlinkGoogleLoginUseCase useCase,
            FakeExternalLoginReadRepository externalLoginReadRepository,
            IExternalLoginRepository externalLoginRepository,
            FakeUserReadRepository userReadRepository,
            FakePasswordRepository passwordRepository,
            SpyUnitOfWork unitOfWork)
        {
            UseCase = useCase;
            ExternalLoginReadRepository = externalLoginReadRepository;
            ExternalLoginRepository = externalLoginRepository as FakeExternalLoginRepository ?? new FakeExternalLoginRepository();
            UserReadRepository = userReadRepository;
            PasswordRepository = passwordRepository;
            UnitOfWork = unitOfWork;
        }

        public UnlinkGoogleLoginUseCase UseCase { get; }

        public FakeExternalLoginReadRepository ExternalLoginReadRepository { get; }

        public FakeExternalLoginRepository ExternalLoginRepository { get; }

        public FakeUserReadRepository UserReadRepository { get; }

        public FakePasswordRepository PasswordRepository { get; }

        public SpyUnitOfWork UnitOfWork { get; }
    }
}
