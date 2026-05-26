using AuthCore.Application.UseCases.Users.RegisterUser;

namespace AuthCore.Application.UnitTests.SmokeTests;

public sealed class ApplicationAssemblySmokeTests
{
    [Fact]
    public void RegisterUserTypes_WhenApplicationProjectIsReferenced_ShouldLoadAssembly()
    {
        var command = new RegisterUserCommand();

        Assert.NotNull(command);
        Assert.Equal(typeof(RegisterUserUseCase).Assembly, typeof(RegisterUserCommand).Assembly);
    }
}
