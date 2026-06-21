using System.Security.Claims;
using AuthCore.Api.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace AuthCore.IntegrationTests.Authentication;

/// <summary>
/// Verifica o mapeamento da identidade Google para o comando de login externo.
/// </summary>
public sealed class GoogleExternalLoginCommandFactoryTests
{
    [Fact]
    public void Create_WhenStandardClaimsExist_ShouldMapCommand()
    {
        var authenticationProperties = new AuthenticationProperties();
        authenticationProperties.Items[ExternalAuthenticationDefaults.ReturnUrlPropertyName] =
            "http://localhost:5173/dashboard";
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, " google-sub-123 "),
            new Claim(ClaimTypes.Email, " bruno@authcore.dev "),
            new Claim("email_verified", "true"),
            new Claim(ClaimTypes.Name, " Bruno Silva "),
            new Claim("picture", " https://lh3.googleusercontent.com/avatar "));
        var metadata = new ExternalLoginRequestMetadata(
            "127.0.0.1",
            "AuthCore.IntegrationTests");
        var factory = new GoogleExternalLoginCommandFactory();

        var command = factory.Create(principal, authenticationProperties, metadata);

        Assert.Equal("google-sub-123", command.ProviderUserId);
        Assert.Equal("bruno@authcore.dev", command.Email);
        Assert.True(command.EmailVerified);
        Assert.Equal("Bruno Silva", command.FullName);
        Assert.Equal("https://lh3.googleusercontent.com/avatar", command.PictureUrl);
        Assert.Equal("http://localhost:5173/dashboard", command.ReturnUrl);
        Assert.Equal("127.0.0.1", command.IpAddress);
        Assert.Equal("AuthCore.IntegrationTests", command.UserAgent);
    }

    [Fact]
    public void Create_WhenGoogleAliasClaimsExist_ShouldMapFallbackValues()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "google-sub-456"),
            new Claim("email", "alias@authcore.dev"),
            new Claim("urn:google:email_verified", "true"),
            new Claim("name", "Alias User"),
            new Claim("urn:google:picture", "https://lh3.googleusercontent.com/alias"));
        var factory = new GoogleExternalLoginCommandFactory();

        var command = factory.Create(
            principal,
            authenticationProperties: null,
            new ExternalLoginRequestMetadata(IpAddress: null, UserAgent: null));

        Assert.Equal("google-sub-456", command.ProviderUserId);
        Assert.Equal("alias@authcore.dev", command.Email);
        Assert.True(command.EmailVerified);
        Assert.Equal("Alias User", command.FullName);
        Assert.Equal("https://lh3.googleusercontent.com/alias", command.PictureUrl);
        Assert.Null(command.ReturnUrl);
        Assert.Null(command.IpAddress);
        Assert.Null(command.UserAgent);
    }

    [Fact]
    public void Create_WhenRequiredClaimsAreMissing_ShouldReturnEmptyRequiredValues()
    {
        var factory = new GoogleExternalLoginCommandFactory();

        var command = factory.Create(
            CreatePrincipal(),
            authenticationProperties: null,
            new ExternalLoginRequestMetadata("127.0.0.1", "AuthCore.IntegrationTests"));

        Assert.Empty(command.ProviderUserId);
        Assert.Empty(command.Email);
        Assert.False(command.EmailVerified);
        Assert.Null(command.FullName);
        Assert.Null(command.PictureUrl);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Google"));
    }
}
