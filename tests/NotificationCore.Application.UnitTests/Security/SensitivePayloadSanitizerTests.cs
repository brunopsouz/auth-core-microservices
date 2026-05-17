using BuildingBlocks.Messaging.Contracts.Security;

namespace NotificationCore.Application.UnitTests.Security;

public sealed class SensitivePayloadSanitizerTests
{
    [Fact]
    public void SanitizeJson_WhenPayloadHasSensitiveVariables_ShouldRedactValues()
    {
        const string payload = """
            {
              "messageId": "e633fd0a-571e-4a02-8d86-bc1bd7a35260",
              "variables": {
                "confirmationCode": "123456",
                "expiresInMinutes": "15"
              },
              "accessToken": "access-token-value"
            }
            """;

        var sanitized = SensitivePayloadSanitizer.SanitizeJson(payload);

        Assert.DoesNotContain("123456", sanitized);
        Assert.DoesNotContain("access-token-value", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
        Assert.Contains("expiresInMinutes", sanitized);
        Assert.Contains("15", sanitized);
    }

    [Fact]
    public void SanitizeText_WhenTextHasSensitiveKeysAndPlainCode_ShouldRedactValues()
    {
        const string text = "Falha com confirmationCode=123456, password: secret e código 654321.";

        var sanitized = SensitivePayloadSanitizer.SanitizeText(text);

        Assert.DoesNotContain("123456", sanitized);
        Assert.DoesNotContain("secret", sanitized);
        Assert.DoesNotContain("654321", sanitized);
        Assert.Contains("confirmationCode=[REDACTED]", sanitized);
        Assert.Contains("password: [REDACTED]", sanitized);
    }

    [Fact]
    public void IsSensitiveKey_WhenKeyHasDifferentCasing_ShouldReturnTrue()
    {
        Assert.True(SensitivePayloadSanitizer.IsSensitiveKey("RefreshToken"));
    }
}
