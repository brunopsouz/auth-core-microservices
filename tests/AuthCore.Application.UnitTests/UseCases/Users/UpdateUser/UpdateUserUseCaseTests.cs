using global::AuthCore.Application.UnitTests.UseCases.Authentication.Support;
using AuthCore.Application.UseCases.Users.UpdateUser;

namespace AuthCore.Application.UnitTests.UseCases.Users.UpdateUser;

public sealed class UpdateUserUseCaseTests
{
    [Fact]
    public async Task Execute_WhenUserIsActive_ShouldUpdateProfile()
    {
        var userReadRepository = new FakeUserReadRepository();
        var userRepository = new FakeUserRepository();
        var user = AuthenticationFixtures.CreateVerifiedUser();
        var useCase = new UpdateUserUseCase(userRepository, userReadRepository);

        userReadRepository.Store(user);

        await useCase.Execute(new global::AuthCore.Application.UseCases.Users.UpdateUser.UpdateUserCommand
        {
            UserIdentifier = user.UserIdentifier,
            FirstName = "Ana",
            LastName = "Souza",
            Contact = "11988887777"
        });

        var updatedUser = Assert.Single(userRepository.UpdatedUsers);

        Assert.Equal("Ana", updatedUser.FirstName);
        Assert.Equal("Souza", updatedUser.LastName);
        Assert.Equal("Ana Souza", updatedUser.FullName);
        Assert.Equal("11988887777", updatedUser.Contact);
    }
}
