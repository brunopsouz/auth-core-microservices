using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationCore.Api.Contracts.Responses;
using NotificationCore.Api.Exceptions;
using NotificationCore.Domain.Common.Exceptions;
using Shared.Observability;

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
    public async Task TryHandleAsync_WhenExceptionIsUnknown_ShouldLogExceptionAndStoreSafeErrorCategory()
    {
        var logger = new CapturingLogger<ApiExceptionHandler>();
        var exceptionHandler = new ApiExceptionHandler(logger);
        var httpContext = CreateHttpContext();
        httpContext.Items[CorrelationIdConstants.HttpContextItemKey] = "corr-notification";

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
            new DomainException("Erro de domínio."),
            CancellationToken.None);

        Assert.True(wasHandled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal("validation", httpContext.Items[RequestLoggingConstants.ErrorCategoryItemKey]);
        Assert.Empty(logger.Entries);
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
