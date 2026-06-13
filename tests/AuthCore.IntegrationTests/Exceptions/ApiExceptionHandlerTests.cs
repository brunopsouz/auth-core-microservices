using AuthCore.Api.Exceptions;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace AuthCore.IntegrationTests.Exceptions;

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
    public async Task TryHandleAsync_WhenExceptionIsInvalidEmailVerificationException_ShouldReturnBadRequestWithGenericMessage()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new InvalidEmailVerificationException(),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal([InvalidEmailVerificationException.InvalidVerificationMessage], response.Errors);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsUnauthorizedException_ShouldReturnUnauthorized()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new UnauthorizedException("Usuário não autenticado."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal(["Usuário não autenticado."], response.Errors);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsForbiddenException_ShouldReturnForbidden()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new ForbiddenException("Usuário sem permissão para a operação."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(["Usuário sem permissão para a operação."], response.Errors);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsNotFoundException_ShouldReturnNotFound()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new NotFoundException("Usuário não encontrado."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal(["Usuário não encontrado."], response.Errors);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsConflictException_ShouldReturnConflict()
    {
        var httpContext = CreateHttpContext();

        var wasHandled = await _exceptionHandler.TryHandleAsync(
            httpContext,
            new ConflictException("Conflito de negócio."),
            CancellationToken.None);

        var response = await ReadResponseAsync(httpContext);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
        Assert.Equal(["Conflito de negócio."], response.Errors);
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
