using AuthCore.Api;
using AuthCore.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthCore.IntegrationTests.Authentication;

/// <summary>
/// Verifica o contrato de rotas publicas e protegidas do fluxo de autenticacao.
/// </summary>
public sealed class AuthFlowRouteContractIntegrationTests
{
    [Fact]
    public async Task Register_WhenMvcIsConfigured_ShouldExposeAuthRegisterAsPostEndpoint()
    {
        var endpoints = await GetRouteEndpointsAsync();

        var endpoint = Assert.Single(endpoints, endpoint => IsRoute(endpoint, "api/auth/register", "POST"));
        var action = endpoint.Metadata.GetRequiredMetadata<ControllerActionDescriptor>();

        Assert.Equal(nameof(AuthController), action.ControllerTypeInfo.Name);
        Assert.Equal(nameof(AuthController.Register), action.ActionName);
        Assert.False(RequiresAuthentication(endpoint));
    }

    [Fact]
    public async Task Register_WhenMvcIsConfigured_ShouldNotExposeUsersPostAsPublicRegistration()
    {
        var endpoints = await GetRouteEndpointsAsync();

        Assert.DoesNotContain(endpoints, endpoint =>
            IsRoute(endpoint, "api/users", "POST") &&
            !RequiresAuthentication(endpoint));
    }

    [Fact]
    public async Task UserController_WhenMvcIsConfigured_ShouldKeepAuthenticatedEndpointsProtected()
    {
        var endpoints = await GetRouteEndpointsAsync();

        AssertProtectedUserRoute(endpoints, "api/users/profile", "GET");
        AssertProtectedUserRoute(endpoints, "api/users/profile", "PUT");
        AssertProtectedUserRoute(endpoints, "api/users/change-password", "PUT");
        AssertProtectedUserRoute(endpoints, "api/users", "DELETE");
    }

    private static async Task<IReadOnlyList<RouteEndpoint>> GetRouteEndpointsAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Issuer"] = "authcore-tests",
            ["Authentication:Jwt:Audience"] = "authcore-tests",
            ["Authentication:Jwt:SigningKey"] = "12345678901234567890123456789012",
            ["Authentication:Jwt:AccessTokenLifetimeMinutes"] = "15",
            ["Authentication:Jwt:RefreshTokenLifetimeDays"] = "7",
            ["Authentication:Jwt:ClockSkewSeconds"] = "60",
            ["Auth:Cookie:SessionCookieName"] = "sid",
            ["Auth:Cookie:Secure"] = "false"
        });

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly);

        builder.Services.AddApi(builder.Configuration);

        await using var app = builder.Build();
        app.MapControllers();
        var endpointRouteBuilder = (IEndpointRouteBuilder)app;

        return endpointRouteBuilder.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static void AssertProtectedUserRoute(
        IReadOnlyList<RouteEndpoint> endpoints,
        string routeTemplate,
        string httpMethod)
    {
        var endpoint = Assert.Single(endpoints, endpoint =>
            IsRoute(endpoint, routeTemplate, httpMethod) &&
            endpoint.Metadata.GetRequiredMetadata<ControllerActionDescriptor>()
                .ControllerTypeInfo.Name == nameof(UserController));

        Assert.True(RequiresAuthentication(endpoint));
    }

    private static bool IsRoute(RouteEndpoint endpoint, string routeTemplate, string httpMethod)
    {
        return string.Equals(endpoint.RoutePattern.RawText, routeTemplate, StringComparison.Ordinal) &&
            endpoint.Metadata
                .GetMetadata<HttpMethodMetadata>()?
                .HttpMethods
                .Contains(httpMethod, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static bool RequiresAuthentication(RouteEndpoint endpoint)
    {
        return endpoint.Metadata
            .OfType<IAuthorizeData>()
            .Any();
    }
}
