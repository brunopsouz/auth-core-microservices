using System.Security.Claims;
using AuthCore.Application.UseCases.Authentication.ExternalLogin;
using Microsoft.AspNetCore.Authentication;

namespace AuthCore.Api.Authentication;

/// <summary>
/// Representa fabrica do comando de conclusao do login externo com Google.
/// </summary>
internal sealed class GoogleExternalLoginCommandFactory : IGoogleExternalLoginCommandFactory
{
    private const string GoogleEmailVerifiedClaimType = "email_verified";
    private const string GooglePictureClaimType = "picture";
    private const string GoogleUrnEmailVerifiedClaimType = "urn:google:email_verified";
    private const string GoogleUrnPictureClaimType = "urn:google:picture";
    private const string SubjectClaimType = "sub";

    /// <inheritdoc />
    public CompleteGoogleLoginCommand Create(
        ClaimsPrincipal principal,
        AuthenticationProperties? authenticationProperties,
        ExternalLoginRequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(metadata);

        return new CompleteGoogleLoginCommand
        {
            ProviderUserId = GetClaimValue(principal, ClaimTypes.NameIdentifier, SubjectClaimType),
            Email = GetClaimValue(principal, ClaimTypes.Email, ClaimTypes.Upn, "email"),
            EmailVerified = GetBooleanClaimValue(
                principal,
                GoogleEmailVerifiedClaimType,
                GoogleUrnEmailVerifiedClaimType),
            FullName = GetOptionalClaimValue(principal, ClaimTypes.Name, "name"),
            PictureUrl = GetOptionalClaimValue(
                principal,
                GooglePictureClaimType,
                GoogleUrnPictureClaimType),
            ReturnUrl = GetReturnUrl(authenticationProperties),
            IpAddress = metadata.IpAddress,
            UserAgent = metadata.UserAgent
        };
    }

    private static string GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        return GetOptionalClaimValue(principal, claimTypes) ?? string.Empty;
    }

    private static string? GetOptionalClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static bool GetBooleanClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        var value = GetOptionalClaimValue(principal, claimTypes);

        return bool.TryParse(value, out var result) && result;
    }

    private static string? GetReturnUrl(AuthenticationProperties? authenticationProperties)
    {
        return authenticationProperties?.Items.TryGetValue(
            ExternalAuthenticationDefaults.ReturnUrlPropertyName,
            out var returnUrl) == true
            ? returnUrl
            : null;
    }
}
