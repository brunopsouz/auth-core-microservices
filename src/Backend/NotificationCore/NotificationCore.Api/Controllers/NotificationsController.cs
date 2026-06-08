using Shared.Messaging.Contracts.Security;
using Microsoft.AspNetCore.Mvc;
using NotificationCore.Api.Contracts.Requests;
using NotificationCore.Api.Contracts.Responses;
using NotificationCore.Application.UseCases.Notifications.Models;
using NotificationCore.Application.UseCases.Notifications.GetNotification;
using NotificationCore.Application.UseCases.Notifications.SearchNotifications;
using NotificationCore.Application.UseCases.Notifications.SendTestEmailNotification;

namespace NotificationCore.Api.Controllers;

/// <summary>
/// Representa controller responsável pelas operações administrativas de notificações.
/// </summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    /// <summary>
    /// Operação para obter uma notificação pelo identificador.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pela consulta.</param>
    /// <param name="id">Identificador da notificação.</param>
    /// <returns>Resposta com os dados da notificação.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ResponseNotificationJson), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResponseNotificationJson>> GetById(
        [FromServices] IGetNotificationUseCase useCase,
        [FromRoute] Guid id)
    {
        var result = await useCase.Execute(new GetNotificationQuery
        {
            NotificationId = id
        });

        if (result is null)
            return NotFound();

        return Ok(MapNotification(result));
    }

    /// <summary>
    /// Operação para buscar notificações por filtros administrativos.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pela busca.</param>
    /// <param name="correlationId">Identificador de correlação opcional.</param>
    /// <param name="status">Status opcional.</param>
    /// <param name="skip">Quantidade de registros ignorados.</param>
    /// <param name="take">Quantidade máxima de registros.</param>
    /// <returns>Resposta com as notificações encontradas.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ResponseSearchNotificationsJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResponseSearchNotificationsJson>> Search(
        [FromServices] ISearchNotificationsUseCase useCase,
        [FromQuery] string? correlationId,
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var result = await useCase.Execute(new SearchNotificationsQuery
        {
            CorrelationId = correlationId,
            Status = status,
            Skip = skip,
            Take = take
        });

        return Ok(new ResponseSearchNotificationsJson
        {
            Notifications = result.Notifications
                .Select(MapNotification)
                .ToList(),
            Skip = result.Skip,
            Take = result.Take
        });
    }

    /// <summary>
    /// Operação para enviar e-mail de teste.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pelo envio de teste.</param>
    /// <param name="request">Dados da requisição de teste.</param>
    /// <returns>Resposta com o resultado do provedor.</returns>
    [HttpPost("test-email")]
    [ProducesResponseType(typeof(ResponseTestEmailNotificationJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResponseTestEmailNotificationJson>> SendTestEmail(
        [FromServices] ISendTestEmailNotificationUseCase useCase,
        [FromBody] RequestTestEmailNotificationJson request)
    {
        var result = await useCase.Execute(new SendTestEmailNotificationCommand
        {
            Recipient = request.Recipient,
            CorrelationId = request.CorrelationId
        });

        return Ok(new ResponseTestEmailNotificationJson
        {
            NotificationId = result.NotificationId,
            CorrelationId = result.CorrelationId,
            Recipient = result.Recipient,
            Provider = result.Provider,
            WasSent = result.WasSent,
            IsTemporaryFailure = result.IsTemporaryFailure,
            ErrorCode = result.ErrorCode,
            ErrorMessage = SensitivePayloadSanitizer.SanitizeText(result.ErrorMessage),
            ProviderMessageId = result.ProviderMessageId
        });
    }


    /// <summary>
    /// Operação para mapear notificação para resposta HTTP.
    /// </summary>
    /// <param name="notification">Notificação da aplicação.</param>
    /// <returns>Resposta HTTP da notificação.</returns>
    private static ResponseNotificationJson MapNotification(NotificationResult notification)
    {
        return new ResponseNotificationJson
        {
            Id = notification.Id,
            Source = notification.Source,
            CorrelationId = notification.CorrelationId,
            IdempotencyKey = notification.IdempotencyKey,
            Channel = notification.Channel,
            Recipient = notification.Recipient,
            TemplateKey = notification.TemplateKey,
            Status = notification.Status,
            Priority = notification.Priority,
            RequestedAtUtc = notification.RequestedAtUtc,
            ScheduledAtUtc = notification.ScheduledAtUtc,
            CreatedAtUtc = notification.CreatedAtUtc,
            SentAtUtc = notification.SentAtUtc,
            FailedAtUtc = notification.FailedAtUtc,
            LastError = SensitivePayloadSanitizer.SanitizeText(notification.LastError),
            DeliveryAttempts = notification.DeliveryAttempts
                .Select(MapDeliveryAttempt)
                .ToList()
        };
    }

    /// <summary>
    /// Operação para mapear tentativa para resposta HTTP.
    /// </summary>
    /// <param name="deliveryAttempt">Tentativa da aplicação.</param>
    /// <returns>Resposta HTTP da tentativa.</returns>
    private static ResponseNotificationDeliveryAttemptJson MapDeliveryAttempt(
        NotificationDeliveryAttemptResult deliveryAttempt)
    {
        return new ResponseNotificationDeliveryAttemptJson
        {
            Id = deliveryAttempt.Id,
            Provider = deliveryAttempt.Provider,
            Status = deliveryAttempt.Status,
            AttemptNumber = deliveryAttempt.AttemptNumber,
            StartedAtUtc = deliveryAttempt.StartedAtUtc,
            FinishedAtUtc = deliveryAttempt.FinishedAtUtc,
            ErrorCode = deliveryAttempt.ErrorCode,
            ErrorMessage = SensitivePayloadSanitizer.SanitizeText(deliveryAttempt.ErrorMessage),
            ProviderMessageId = deliveryAttempt.ProviderMessageId
        };
    }

}
