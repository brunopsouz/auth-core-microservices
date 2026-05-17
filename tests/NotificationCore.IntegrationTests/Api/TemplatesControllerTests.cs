using Microsoft.AspNetCore.Mvc;
using NotificationCore.Api.Contracts.Responses;
using NotificationCore.Api.Controllers;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Infrastructure.Notifications.Templates;

namespace NotificationCore.IntegrationTests.Api;

public sealed class TemplatesControllerTests
{
    [Fact]
    public async Task ListActive_WhenTemplatesExist_ShouldMapResponse()
    {
        var repository = new StubNotificationTemplateRepository
        {
            Templates =
            [
                new NotificationTemplate
                {
                    TemplateKey = "auth.email-confirmation",
                    Channel = NotificationChannel.Email,
                    Subject = "Confirme seu e-mail",
                    HtmlBody = "<p>Seu código é {{confirmationCode}}</p>",
                    TextBody = "Seu código é {{confirmationCode}}"
                }
            ]
        };
        var controller = new TemplatesController();

        var actionResult = await controller.ListActive(repository);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsAssignableFrom<IReadOnlyCollection<ResponseNotificationTemplateJson>>(okResult.Value);
        var template = Assert.Single(response);

        Assert.True(repository.WasListed);
        Assert.Equal("auth.email-confirmation", template.TemplateKey);
        Assert.Equal("Email", template.Channel);
        Assert.Equal("Confirme seu e-mail", template.Subject);
    }

    private sealed class StubNotificationTemplateRepository : INotificationTemplateRepository
    {
        public IReadOnlyCollection<NotificationTemplate> Templates { get; init; } = [];

        public bool WasListed { get; private set; }

        public Task<IReadOnlyCollection<NotificationTemplate>> ListActiveAsync()
        {
            WasListed = true;

            return Task.FromResult(Templates);
        }

        public Task<NotificationTemplate?> GetActiveAsync(string templateKey, NotificationChannel channel)
        {
            return Task.FromResult(Templates.FirstOrDefault(template =>
                template.TemplateKey == templateKey &&
                template.Channel == channel));
        }
    }
}
