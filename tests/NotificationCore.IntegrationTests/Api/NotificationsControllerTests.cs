using Microsoft.AspNetCore.Mvc;
using NotificationCore.Api.Contracts.Requests;
using NotificationCore.Api.Contracts.Responses;
using NotificationCore.Api.Controllers;
using NotificationCore.Application.UseCases.Notifications.Models;
using NotificationCore.Application.UseCases.Notifications.GetNotification;
using NotificationCore.Application.UseCases.Notifications.SearchNotifications;
using NotificationCore.Application.UseCases.Notifications.SendTestEmailNotification;

namespace NotificationCore.IntegrationTests.Api;

public sealed class NotificationsControllerTests
{
    [Fact]
    public async Task GetById_WhenNotificationExists_ShouldMapResultToResponse()
    {
        var notificationId = Guid.NewGuid();
        var useCase = new StubGetNotificationUseCase
        {
            Result = CreateNotificationResult(notificationId)
        };
        var controller = new NotificationsController();

        var actionResult = await controller.GetById(useCase, notificationId);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ResponseNotificationJson>(okResult.Value);
        var attempt = Assert.Single(response.DeliveryAttempts);

        Assert.Equal(notificationId, useCase.Query!.NotificationId);
        Assert.Equal(notificationId, response.Id);
        Assert.Equal("AuthCore", response.Source);
        Assert.Equal("correlation-123", response.CorrelationId);
        Assert.Equal("auth.email-confirmation", response.TemplateKey);
        Assert.Equal("Sent", response.Status);
        Assert.Equal("Smtp", attempt.Provider);
        Assert.Equal("Succeeded", attempt.Status);
    }

    [Fact]
    public async Task GetById_WhenNotificationDoesNotExist_ShouldReturnNotFound()
    {
        var useCase = new StubGetNotificationUseCase();
        var controller = new NotificationsController();

        var actionResult = await controller.GetById(useCase, Guid.NewGuid());

        Assert.IsType<NotFoundResult>(actionResult.Result);
    }

    [Fact]
    public async Task Search_WhenFiltersAreProvided_ShouldMapQueryAndResponse()
    {
        var notificationId = Guid.NewGuid();
        var useCase = new StubSearchNotificationsUseCase
        {
            Result = new SearchNotificationsResult
            {
                Notifications = [CreateNotificationResult(notificationId)],
                Skip = 10,
                Take = 20
            }
        };
        var controller = new NotificationsController();

        var actionResult = await controller.Search(
            useCase,
            correlationId: "correlation-123",
            status: "Sent",
            skip: 10,
            take: 20);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ResponseSearchNotificationsJson>(okResult.Value);
        var notification = Assert.Single(response.Notifications);

        Assert.Equal("correlation-123", useCase.Query!.CorrelationId);
        Assert.Equal("Sent", useCase.Query.Status);
        Assert.Equal(10, useCase.Query.Skip);
        Assert.Equal(20, useCase.Query.Take);
        Assert.Equal(10, response.Skip);
        Assert.Equal(20, response.Take);
        Assert.Equal(notificationId, notification.Id);
    }

    [Fact]
    public async Task SendTestEmail_WhenRequestIsValid_ShouldMapCommandAndResponse()
    {
        var notificationId = Guid.NewGuid();
        var useCase = new StubSendTestEmailNotificationUseCase
        {
            Result = new SendTestEmailNotificationResult
            {
                NotificationId = notificationId,
                CorrelationId = "test-correlation",
                Recipient = "admin@example.com",
                Provider = "Smtp",
                WasSent = true,
                ProviderMessageId = "smtp-message-123"
            }
        };
        var controller = new NotificationsController();

        var actionResult = await controller.SendTestEmail(
            useCase,
            new RequestTestEmailNotificationJson
            {
                Recipient = "admin@example.com",
                CorrelationId = "test-correlation"
            });

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ResponseTestEmailNotificationJson>(okResult.Value);

        Assert.Equal("admin@example.com", useCase.Command!.Recipient);
        Assert.Equal("test-correlation", useCase.Command.CorrelationId);
        Assert.Equal(notificationId, response.NotificationId);
        Assert.Equal("Smtp", response.Provider);
        Assert.True(response.WasSent);
        Assert.Equal("smtp-message-123", response.ProviderMessageId);
    }

    private static NotificationResult CreateNotificationResult(Guid notificationId)
    {
        var attemptId = Guid.NewGuid();
        var requestedAtUtc = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);

        return new NotificationResult
        {
            Id = notificationId,
            Source = "AuthCore",
            CorrelationId = "correlation-123",
            IdempotencyKey = $"auth-email-confirmation:{Guid.NewGuid()}",
            Channel = "Email",
            Recipient = "bruno@example.com",
            TemplateKey = "auth.email-confirmation",
            Status = "Sent",
            Priority = "High",
            RequestedAtUtc = requestedAtUtc,
            ScheduledAtUtc = requestedAtUtc,
            CreatedAtUtc = requestedAtUtc,
            SentAtUtc = requestedAtUtc.AddSeconds(3),
            DeliveryAttempts =
            [
                new NotificationDeliveryAttemptResult
                {
                    Id = attemptId,
                    Provider = "Smtp",
                    Status = "Succeeded",
                    AttemptNumber = 1,
                    StartedAtUtc = requestedAtUtc.AddSeconds(1),
                    FinishedAtUtc = requestedAtUtc.AddSeconds(3),
                    ProviderMessageId = "smtp-message-123"
                }
            ]
        };
    }

    private sealed class StubGetNotificationUseCase : IGetNotificationUseCase
    {
        public GetNotificationQuery? Query { get; private set; }

        public NotificationResult? Result { get; init; }

        public Task<NotificationResult?> Execute(GetNotificationQuery query)
        {
            Query = query;

            return Task.FromResult(Result);
        }
    }

    private sealed class StubSearchNotificationsUseCase : ISearchNotificationsUseCase
    {
        public SearchNotificationsQuery? Query { get; private set; }

        public SearchNotificationsResult Result { get; init; } = new();

        public Task<SearchNotificationsResult> Execute(SearchNotificationsQuery query)
        {
            Query = query;

            return Task.FromResult(Result);
        }
    }

    private sealed class StubSendTestEmailNotificationUseCase : ISendTestEmailNotificationUseCase
    {
        public SendTestEmailNotificationCommand? Command { get; private set; }

        public SendTestEmailNotificationResult Result { get; init; } = new();

        public Task<SendTestEmailNotificationResult> Execute(SendTestEmailNotificationCommand command)
        {
            Command = command;

            return Task.FromResult(Result);
        }
    }
}
