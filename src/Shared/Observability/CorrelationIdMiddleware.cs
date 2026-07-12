using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Shared.Observability;

/// <summary>
/// Representa middleware para validar, gerar e propagar o identificador de correlação HTTP.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const int MaximumCorrelationIdLength = 128;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="next">Próximo middleware do pipeline.</param>
    /// <param name="logger">Logger usado para criar escopo estruturado.</param>
    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Operação para aplicar o identificador de correlação à requisição atual.
    /// </summary>
    /// <param name="context">Contexto HTTP atual.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = GetOrCreateCorrelationId(context.Request.Headers[CorrelationIdConstants.HeaderName]);

        context.Items[CorrelationIdConstants.HttpContextItemKey] = correlationId;
        context.Request.Headers[CorrelationIdConstants.HeaderName] = correlationId;
        context.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId;
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            if (httpContext.Items.TryGetValue(CorrelationIdConstants.HttpContextItemKey, out var value)
                && value is string correlationId)
            {
                httpContext.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId;
            }

            return Task.CompletedTask;
        }, context);

        Activity.Current?.SetTag(CorrelationIdConstants.ActivityTagName, correlationId);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            [CorrelationIdConstants.LogScopePropertyName] = correlationId
        });

        await _next(context);
    }

    private static string GetOrCreateCorrelationId(StringValues values)
    {
        if (values.Count == 1 && IsValid(values[0]))
        {
            return values[0]!;
        }

        return Guid.NewGuid().ToString("D");
    }

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumCorrelationIdLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!IsAllowedCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowedCharacter(char character)
    {
        return character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '.'
            or '_'
            or '-';
    }
}
