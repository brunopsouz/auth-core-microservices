using AuthCore.Application.Common.Exceptions;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;

namespace AuthCore.Application.UnitTests.UseCases.Authentication.ExternalLogin;

public sealed class ExternalReturnUrlValidatorTests
{
    [Fact]
    public void Validate_WhenReturnUrlIsAllowed_ShouldReturnUrl()
    {
        var validator = CreateValidator("https://app.seudominio.com");

        var result = validator.Validate(" https://app.seudominio.com/auth/callback?next=/home ");

        Assert.Equal("https://app.seudominio.com/auth/callback?next=/home", result);
    }

    [Fact]
    public void Validate_WhenReturnUrlIsExternal_ShouldThrowValidationException()
    {
        var validator = CreateValidator("https://app.seudominio.com");

        var exception = Assert.Throws<ValidationException>(() => validator.Validate("https://site-malicioso.com/auth/callback"));

        Assert.Equal("A URL de retorno informada não é permitida.", exception.Message);
    }

    [Fact]
    public void Validate_WhenReturnUrlUsesJavascriptScheme_ShouldThrowValidationException()
    {
        var validator = CreateValidator("https://app.seudominio.com");

        var exception = Assert.Throws<ValidationException>(() => validator.Validate("javascript:alert(1)"));

        Assert.Equal("A URL de retorno informada não é permitida.", exception.Message);
    }

    [Fact]
    public void Validate_WhenReturnUrlUsesDataScheme_ShouldThrowValidationException()
    {
        var validator = CreateValidator("https://app.seudominio.com");

        var exception = Assert.Throws<ValidationException>(() => validator.Validate("data:text/html,<h1>x</h1>"));

        Assert.Equal("A URL de retorno informada não é permitida.", exception.Message);
    }

    [Fact]
    public void Validate_WhenReturnUrlIsProtocolRelative_ShouldThrowValidationException()
    {
        var validator = CreateValidator("https://app.seudominio.com");

        var exception = Assert.Throws<ValidationException>(() => validator.Validate("//site-malicioso.com/auth/callback"));

        Assert.Equal("A URL de retorno informada não é permitida.", exception.Message);
    }

    [Fact]
    public void Validate_WhenReturnUrlIsEmpty_ShouldReturnDefaultReturnUrl()
    {
        var validator = CreateValidator("https://app.seudominio.com", defaultReturnUrl: "/auth/error");

        var result = validator.Validate(" ");

        Assert.Equal("/auth/error", result);
    }

    [Fact]
    public void Validate_WhenDefaultReturnUrlIsAbsoluteAndAllowed_ShouldReturnDefaultReturnUrl()
    {
        var validator = CreateValidator(
            "https://app.seudominio.com",
            defaultReturnUrl: " https://app.seudominio.com/auth/error ");

        var result = validator.Validate(null);

        Assert.Equal("https://app.seudominio.com/auth/error", result);
    }

    [Fact]
    public void Validate_WhenDefaultReturnUrlIsProtocolRelativeWithBackslash_ShouldThrowValidationException()
    {
        var validator = CreateValidator("https://app.seudominio.com", defaultReturnUrl: @"/\site-malicioso.com");

        var exception = Assert.Throws<ValidationException>(() => validator.Validate(null));

        Assert.Equal("A URL de retorno informada não é permitida.", exception.Message);
    }

    private static ExternalReturnUrlValidator CreateValidator(
        string allowedReturnUrl,
        string defaultReturnUrl = "/")
    {
        return new ExternalReturnUrlValidator(new StubExternalAuthenticationOptionsProvider(
            new ExternalAuthenticationOptions
            {
                DefaultReturnUrl = defaultReturnUrl,
                AllowedReturnUrls = [allowedReturnUrl]
            }));
    }

    private sealed class StubExternalAuthenticationOptionsProvider : IExternalAuthenticationOptionsProvider
    {
        private readonly ExternalAuthenticationOptions _options;

        public StubExternalAuthenticationOptionsProvider(ExternalAuthenticationOptions options)
        {
            _options = options;
        }

        public ExternalAuthenticationOptions GetOptions()
        {
            return _options;
        }
    }
}
