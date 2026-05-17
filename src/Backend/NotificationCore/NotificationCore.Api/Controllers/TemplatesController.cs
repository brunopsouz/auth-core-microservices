using Microsoft.AspNetCore.Mvc;
using NotificationCore.Api.Contracts.Responses;
using NotificationCore.Infrastructure.Notifications.Templates;

namespace NotificationCore.Api.Controllers;

/// <summary>
/// Representa controller responsável pelas operações administrativas de templates.
/// </summary>
[ApiController]
[Route("api/templates")]
public sealed class TemplatesController : ControllerBase
{
    /// <summary>
    /// Operação para listar templates ativos.
    /// </summary>
    /// <param name="templateRepository">Repositório responsável pela leitura de templates.</param>
    /// <returns>Resposta com templates ativos.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ResponseNotificationTemplateJson>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ResponseNotificationTemplateJson>>> ListActive(
        [FromServices] INotificationTemplateRepository templateRepository)
    {
        var templates = await templateRepository.ListActiveAsync();

        return Ok(templates
            .Select(MapTemplate)
            .ToList());
    }

    #region Helpers

    /// <summary>
    /// Operação para mapear template para resposta HTTP.
    /// </summary>
    /// <param name="template">Template da infraestrutura.</param>
    /// <returns>Resposta HTTP do template.</returns>
    private static ResponseNotificationTemplateJson MapTemplate(NotificationTemplate template)
    {
        return new ResponseNotificationTemplateJson
        {
            TemplateKey = template.TemplateKey,
            Channel = template.Channel.ToString(),
            Subject = template.Subject,
            HtmlBody = template.HtmlBody,
            TextBody = template.TextBody
        };
    }

    #endregion
}
