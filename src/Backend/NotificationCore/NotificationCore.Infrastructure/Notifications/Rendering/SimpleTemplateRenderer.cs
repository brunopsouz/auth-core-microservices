using System.Text;
using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Domain.Notifications.Rendering;
using NotificationCore.Infrastructure.Notifications.Templates;

namespace NotificationCore.Infrastructure.Notifications.Rendering;

/// <summary>
/// Representa renderizador simples de templates de notificação.
/// </summary>
internal sealed class SimpleTemplateRenderer : ITemplateRenderer
{
    private const string TEMPLATE_NOT_FOUND_MESSAGE = "Template ativo não encontrado.";
    private const string INVALID_VARIABLE_MESSAGE = "Template possui variável inválida.";
    private const string MISSING_VARIABLE_MESSAGE = "Template possui variável obrigatória ausente.";

    private readonly INotificationTemplateRepository _templateRepository;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="templateRepository">Repositório de templates.</param>
    public SimpleTemplateRenderer(INotificationTemplateRepository templateRepository)
    {
        ArgumentNullException.ThrowIfNull(templateRepository);

        _templateRepository = templateRepository;
    }

    #endregion

    /// <summary>
    /// Operação para renderizar template de notificação.
    /// </summary>
    /// <param name="templateKey">Chave do template.</param>
    /// <param name="channel">Canal da notificação.</param>
    /// <param name="variables">Variáveis usadas na renderização.</param>
    /// <returns>Template renderizado.</returns>
    public async Task<RenderedTemplate> RenderAsync(
        string templateKey,
        NotificationChannel channel,
        IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var template = await _templateRepository.GetActiveAsync(templateKey, channel);

        DomainException.When(template is null, TEMPLATE_NOT_FOUND_MESSAGE);

        return new RenderedTemplate
        {
            Subject = RenderContent(template!.Subject, variables),
            HtmlBody = RenderContent(template.HtmlBody, variables),
            TextBody = RenderContent(template.TextBody, variables)
        };
    }

    #region Helpers

    /// <summary>
    /// Operação para renderizar conteúdo textual com variáveis.
    /// </summary>
    /// <param name="content">Conteúdo do template.</param>
    /// <param name="variables">Variáveis usadas na renderização.</param>
    /// <returns>Conteúdo renderizado.</returns>
    private static string RenderContent(string content, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var rendered = new StringBuilder(content.Length);
        var currentIndex = 0;

        while (currentIndex < content.Length)
        {
            var variableStartIndex = content.IndexOf("{{", currentIndex, StringComparison.Ordinal);

            if (variableStartIndex < 0)
            {
                rendered.Append(content, currentIndex, content.Length - currentIndex);
                break;
            }

            var variableEndIndex = content.IndexOf("}}", variableStartIndex + 2, StringComparison.Ordinal);

            DomainException.When(variableEndIndex < 0, INVALID_VARIABLE_MESSAGE);

            rendered.Append(content, currentIndex, variableStartIndex - currentIndex);

            var variableName = content[(variableStartIndex + 2)..variableEndIndex].Trim();

            DomainException.When(string.IsNullOrWhiteSpace(variableName), INVALID_VARIABLE_MESSAGE);
            DomainException.When(!variables.TryGetValue(variableName, out var variableValue), MISSING_VARIABLE_MESSAGE);

            rendered.Append(variableValue);
            currentIndex = variableEndIndex + 2;
        }

        return rendered.ToString();
    }

    #endregion
}
