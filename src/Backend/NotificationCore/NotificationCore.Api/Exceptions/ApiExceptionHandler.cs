using BuildingBlocks.Messaging.Contracts.Security;
using System.Net.Mime;
using Microsoft.AspNetCore.Diagnostics;
using NotificationCore.Api.Contracts.Responses;
using NotificationCore.Domain.Common.Exceptions;

namespace NotificationCore.Api.Exceptions;

/// <summary>
/// Representa handler global para exceções da API.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private const string UNKNOWN_ERROR_MESSAGE = "Ocorreu um erro interno inesperado.";

    private readonly ILogger<ApiExceptionHandler> _logger;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="logger">Serviço de logging da aplicação.</param>
    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    #endregion

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

        var (statusCode, errors) = MapException(exception);

        LogException(exception, statusCode);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = MediaTypeNames.Application.Json;

        await httpContext.Response.WriteAsJsonAsync(new ResponseErrorJson
        {
            Errors = errors
        }, cancellationToken);

        return true;
    }

    #region Helpers

    /// <summary>
    /// Operação para mapear a exceção para o status code e mensagens de erro da resposta.
    /// </summary>
    /// <param name="exception">Exceção capturada no pipeline.</param>
    /// <returns>Status code e mensagens de erro da resposta.</returns>
    private static (int StatusCode, IList<string> Errors) MapException(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException unauthorizedAccessException => (StatusCodes.Status401Unauthorized, GetErrors(unauthorizedAccessException)),
            DomainException domainException => (StatusCodes.Status400BadRequest, GetErrors(domainException)),
            _ => (StatusCodes.Status500InternalServerError, [UNKNOWN_ERROR_MESSAGE])
        };
    }

    /// <summary>
    /// Operação para registrar a exceção tratada.
    /// </summary>
    /// <param name="exception">Exceção capturada no pipeline.</param>
    /// <param name="statusCode">Status code mapeado para a resposta.</param>
    private void LogException(Exception exception, int statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                "Erro interno não tratado durante o processamento da requisição. ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, ExceptionDetails={ExceptionDetails}.",
                exception.GetType().Name,
                SensitivePayloadSanitizer.SanitizeText(exception.Message),
                SensitivePayloadSanitizer.SanitizeText(exception.ToString()));
            return;
        }

        _logger.LogWarning(
            "Exceção tratada pela API com status code {StatusCode}. ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, ExceptionDetails={ExceptionDetails}.",
            statusCode,
            exception.GetType().Name,
            SensitivePayloadSanitizer.SanitizeText(exception.Message),
            SensitivePayloadSanitizer.SanitizeText(exception.ToString()));
    }

    /// <summary>
    /// Operação para obter a lista de mensagens de erro da exceção.
    /// </summary>
    /// <param name="exception">Exceção tratada pela API.</param>
    /// <returns>Lista com as mensagens de erro da exceção.</returns>
    private static IList<string> GetErrors(Exception exception)
    {
        return [SensitivePayloadSanitizer.SanitizeText(exception.Message)];
    }

    #endregion
}
