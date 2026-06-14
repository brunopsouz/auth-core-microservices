using Microsoft.AspNetCore.Mvc;
using NotificationCore.Api.Contracts.Responses;
using NotificationCore.Api.Controllers;
using NotificationCore.Application.UseCases.Notifications.ListActiveNotificationTemplates;

namespace NotificationCore.IntegrationTests.Api;

public sealed class TemplatesControllerTests
{
    [Fact]
    public async Task ListActive_WhenTemplatesExist_ShouldMapResponse()
    {
        var useCase = new StubListActiveNotificationTemplatesUseCase
        {
            Templates =
            [
                new ListActiveNotificationTemplateResult
                {
                    TemplateKey = "auth.email-confirmation",
                    Channel = "Email",
                    Subject = "Confirme seu e-mail",
                    HtmlBody = "<p>Seu codigo e {{confirmationCode}}</p>",
                    TextBody = "Seu codigo e {{confirmationCode}}"
                }
            ]
        };
        var controller = new TemplatesController();

        var actionResult = await controller.ListActive(useCase);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsAssignableFrom<IReadOnlyCollection<ResponseNotificationTemplateJson>>(okResult.Value);
        var template = Assert.Single(response);

        Assert.True(useCase.WasListed);
        Assert.Equal("auth.email-confirmation", template.TemplateKey);
        Assert.Equal("Email", template.Channel);
        Assert.Equal("Confirme seu e-mail", template.Subject);
    }

    private sealed class StubListActiveNotificationTemplatesUseCase : IListActiveNotificationTemplatesUseCase
    {
        public IReadOnlyCollection<ListActiveNotificationTemplateResult> Templates { get; init; } = [];

        public bool WasListed { get; private set; }

        public Task<IReadOnlyCollection<ListActiveNotificationTemplateResult>> Execute()
        {
            WasListed = true;

            return Task.FromResult(Templates);
        }
    }
}
