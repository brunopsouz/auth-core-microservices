using AuthCore.Api.Exceptions;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Observability;
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

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsUnknown_ShouldLogExceptionAndStoreSafeErrorCategory()
    {
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var exceptionHandler = new ApiExceptionHandler(logger);
        var httpContext = CreateHttpContext();
        httpContext.Items[CorrelationIdConstants.HttpContextItemKey] = "corr-auth";

        var wasHandled = await exceptionHandler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("Erro interno com person@example.com."),
            CancellationToken.None);

        var responseText = await ReadResponseTextAsync(httpContext);
        var entry = Assert.Single(logger.Entries);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("unexpected", httpContext.Items[RequestLoggingConstants.ErrorCategoryItemKey]);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.DoesNotContain("InvalidOperationException", responseText);
        Assert.DoesNotContain("person@example.com", responseText);
        Assert.DoesNotContain(" at ", responseText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsKnown_ShouldStoreSafeErrorCategoryWithoutTechnicalLog()
    {
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var exceptionHandler = new ApiExceptionHandler(logger);
        var httpContext = CreateHttpContext();

        var wasHandled = await exceptionHandler.TryHandleAsync(
            httpContext,
            new NotFoundException("Usuário não encontrado."),
            CancellationToken.None);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal("not_found", httpContext.Items[RequestLoggingConstants.ErrorCategoryItemKey]);
        Assert.Empty(logger.Entries);
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

    private static async Task<string> ReadResponseTextAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;

        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<CapturedLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new CapturedLogEntry(logLevel, exception));
        }
    }

    private sealed class CapturedLogEntry
    {
        public CapturedLogEntry(LogLevel level, Exception? exception)
        {
            Level = level;
            Exception = exception;
        }

        public LogLevel Level { get; }

        public Exception? Exception { get; }
    }

}
