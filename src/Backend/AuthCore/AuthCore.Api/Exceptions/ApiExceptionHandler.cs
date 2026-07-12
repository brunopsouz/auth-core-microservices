using System.Diagnostics;
using System.Net.Sockets;
using System.Net.Mime;
using System.Security.Authentication;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Shared.Observability;

namespace AuthCore.Api.Exceptions;

/// <summary>
/// Representa handler global para exceções da API.
/// </summary>
internal sealed class ApiExceptionHandler : IExceptionHandler
{
    private const string UnknownErrorMessage = "Ocorreu um erro interno inesperado.";

    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<ApiExceptionHandler> _logger;
    private readonly UnhandledExceptionMetrics _unhandledExceptionMetrics;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="logger">Serviço de logging da aplicação.</param>
    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
        : this(logger, new UnhandledExceptionMetrics())
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="logger">Serviço de logging da aplicação.</param>
    /// <param name="unhandledExceptionMetrics">Métricas de exceções inesperadas.</param>
    public ApiExceptionHandler(
        ILogger<ApiExceptionHandler> logger,
        UnhandledExceptionMetrics unhandledExceptionMetrics)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(unhandledExceptionMetrics);

        _logger = logger;
        _unhandledExceptionMetrics = unhandledExceptionMetrics;
    }

    /// <summary>
    /// Operação para tratar exceções não tratadas da requisição atual.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP da requisição.</param>
    /// <param name="exception">Exceção capturada no pipeline.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>Resultado do tratamento da exceção.</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is AuthCoreException authCoreException)
        {
            await HandleProjectExceptionAsync(httpContext, authCoreException, cancellationToken);
        }
        else
        {
            RecordUnhandledException(httpContext, exception);
            await HandleUnknownExceptionAsync(httpContext, cancellationToken);
        }

        var statusCode = httpContext.Response.StatusCode;
        httpContext.Items[RequestLoggingConstants.ErrorCategoryItemKey] = ResolveErrorCategory(statusCode);
        LogUnexpectedException(exception, httpContext, statusCode);

        return true;
    }

    private void RecordUnhandledException(HttpContext httpContext, Exception exception)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error);
        _unhandledExceptionMetrics.Record(httpContext, ClassifyUnexpectedException(exception));
    }

    private static async Task HandleProjectExceptionAsync(
        HttpContext httpContext,
        AuthCoreException authCoreException,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = (int)authCoreException.GetStatusCode();
        httpContext.Response.ContentType = MediaTypeNames.Application.Json;

        await httpContext.Response.WriteAsJsonAsync(new ResponseErrorJson
        {
            Errors = authCoreException.GetErrorMessages()
        }, cancellationToken);
    }

    private static async Task HandleUnknownExceptionAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = MediaTypeNames.Application.Json;

        await httpContext.Response.WriteAsJsonAsync(new ResponseErrorJson
        {
            Errors = [UnknownErrorMessage]
        }, cancellationToken);
    }

    private void LogUnexpectedException(Exception exception, HttpContext httpContext, int statusCode)
    {
        if (statusCode < StatusCodes.Status500InternalServerError)
        {
            return;
        }

        var activity = Activity.Current;

        _logger.LogError(
            exception,
            "Erro interno não tratado durante o processamento da requisição. ExceptionType={ExceptionType}, StatusCode={StatusCode}, CorrelationId={CorrelationId}, TraceId={TraceId}, SpanId={SpanId}.",
            exception.GetType().Name,
            statusCode,
            GetCorrelationId(httpContext),
            activity?.TraceId.ToString(),
            activity?.SpanId.ToString());
    }

    private static string ResolveErrorCategory(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "validation",
            StatusCodes.Status401Unauthorized => "authentication",
            StatusCodes.Status403Forbidden => "authorization",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status409Conflict => "conflict",
            >= StatusCodes.Status500InternalServerError => "unexpected",
            _ => "handled"
        };
    }

    private static string ClassifyUnexpectedException(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "cancelled",
            TimeoutException => "timeout",
            SocketException or IOException => "connectivity",
            AuthenticationException => "authentication",
            InvalidOperationException or FormatException or ArgumentException => "protocol",
            _ => "unknown"
        };
    }

    private static string? GetCorrelationId(HttpContext httpContext)
    {
        return httpContext.Items.TryGetValue(CorrelationIdConstants.HttpContextItemKey, out var value)
            && value is string correlationId
                ? correlationId
                : null;
    }
}
