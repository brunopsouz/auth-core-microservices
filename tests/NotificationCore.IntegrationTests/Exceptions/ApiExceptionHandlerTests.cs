using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationCore.Api.Contracts.Responses;
using NotificationCore.Api.Exceptions;
using NotificationCore.Domain.Common.Exceptions;

namespace NotificationCore.IntegrationTests.Exceptions;

public sealed class ApiExceptionHandlerTests
{
    /// <summary>
    /// Campo que armazena exception handler.
    /// </summary>
    private readonly ApiExceptionHandler _exceptionHandler = new(NullLogger<ApiExceptionHandler>.Instance);

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsDomainException_ShouldReturnBadRequest()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new DomainException("Erro de domínio."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal(["Erro de domínio."], response.Errors);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsUnknown_ShouldReturnInternalServerError()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("Erro interno."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal(["Ocorreu um erro interno inesperado."], response.Errors);
    }

    [Fact]
    public async Task TryHandleAsync_WhenDomainExceptionHasSensitiveData_ShouldReturnSanitizedError()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new DomainException("Falha no confirmationCode=123456."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);
        var error = Assert.Single(response.Errors);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.DoesNotContain("123456", error);
        Assert.Contains("confirmationCode=[REDACTED]", error);
    }


    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static async Task<ResponseErrorJson> ReadResponseAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;

        return (await JsonSerializer.DeserializeAsync<ResponseErrorJson>(
            httpContext.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken: CancellationToken.None))!;
    }

}
