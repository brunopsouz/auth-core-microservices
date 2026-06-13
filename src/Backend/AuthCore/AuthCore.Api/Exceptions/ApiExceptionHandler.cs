using AuthCore.Api.Contracts.Responses;
using AuthCore.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using System.Net.Mime;

namespace AuthCore.Api.Exceptions;

/// <summary>
/// Representa handler global para exceções da API.
/// </summary>
internal sealed class ApiExceptionHandler : IExceptionHandler
{
    private const string UNKNOWN_ERROR_MESSAGE = "Ocorreu um erro interno inesperado.";

    /// <summary>
    /// Campo que armazena logger.
    /// </summary>
    private readonly ILogger<ApiExceptionHandler> _logger;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="logger">Serviço de logging da aplicação.</param>
    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
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
            await HandleProjectExceptionAsync(httpContext, authCoreException, cancellationToken);
        else
            await HandleUnknownExceptionAsync(httpContext, cancellationToken);

        LogException(exception, httpContext.Response.StatusCode);

        return true;
    }

    /// <summary>
    /// Operação para tratar exceção conhecida do projeto.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP da requisição.</param>
    /// <param name="authCoreException">Exceção conhecida do projeto.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
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

    /// <summary>
    /// Operação para tratar exceção desconhecida.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP da requisição.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    private static async Task HandleUnknownExceptionAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = MediaTypeNames.Application.Json;

        await httpContext.Response.WriteAsJsonAsync(new ResponseErrorJson
        {
            Errors = [UNKNOWN_ERROR_MESSAGE]
        }, cancellationToken);
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
            _logger.LogError(exception, "Erro interno não tratado durante o processamento da requisição.");
            return;
        }

        _logger.LogWarning(exception, "Exceção tratada pela API com status code {StatusCode}.", statusCode);
    }
}
