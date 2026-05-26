using NotificationCore.Domain.Common.Exceptions;
using NotificationCore.Domain.Notifications.Enums;
using NotificationCore.Infrastructure.Notifications.Rendering;
using NotificationCore.Infrastructure.Notifications.Templates;

namespace NotificationCore.IntegrationTests.Infrastructure;

public sealed class SimpleTemplateRendererTests
{
    [Fact]
    public async Task RenderAsync_WhenVariablesAreAvailable_ShouldRenderTemplate()
    {
        var templateRepository = new FakeNotificationTemplateRepository();
        var renderer = new SimpleTemplateRenderer(templateRepository);

        templateRepository.Store(new NotificationTemplate
        {
            TemplateKey = "auth.email-confirmation",
            Channel = NotificationChannel.Email,
            Subject = "Confirme o código {{confirmationCode}}",
            HtmlBody = "<p>Código <strong>{{confirmationCode}}</strong> expira em {{ expiresInMinutes }} minutos.</p>",
            TextBody = "Código {{confirmationCode}} expira em {{expiresInMinutes}} minutos."
        });

        var result = await renderer.RenderAsync(
            "auth.email-confirmation",
            NotificationChannel.Email,
            new Dictionary<string, string>
            {
                ["confirmationCode"] = "123456",
                ["expiresInMinutes"] = "15"
            });

        Assert.Equal("Confirme o código 123456", result.Subject);
        Assert.Equal("<p>Código <strong>123456</strong> expira em 15 minutos.</p>", result.HtmlBody);
        Assert.Equal("Código 123456 expira em 15 minutos.", result.TextBody);
    }

    [Fact]
    public async Task RenderAsync_WhenVariableIsMissing_ShouldFailWithoutLeakingSensitiveData()
    {
        var templateRepository = new FakeNotificationTemplateRepository();
        var renderer = new SimpleTemplateRenderer(templateRepository);

        templateRepository.Store(new NotificationTemplate
        {
            TemplateKey = "auth.email-confirmation",
            Channel = NotificationChannel.Email,
            Subject = "Confirme seu e-mail",
            HtmlBody = "<p>Código <strong>{{confirmationCode}}</strong>.</p>",
            TextBody = "Código {{confirmationCode}}."
        });

        var exception = await Assert.ThrowsAsync<DomainException>(() => renderer.RenderAsync(
            "auth.email-confirmation",
            NotificationChannel.Email,
            new Dictionary<string, string>
            {
                ["expiresInMinutes"] = "15"
            }));

        Assert.Equal("Template possui variável obrigatória ausente.", exception.Message);
        Assert.DoesNotContain("confirmationCode", exception.Message);
        Assert.DoesNotContain("123456", exception.Message);
    }

    [Fact]
    public async Task RenderAsync_WhenTemplateDoesNotExist_ShouldFailControlled()
    {
        var templateRepository = new FakeNotificationTemplateRepository();
        var renderer = new SimpleTemplateRenderer(templateRepository);

        var exception = await Assert.ThrowsAsync<DomainException>(() => renderer.RenderAsync(
            "auth.email-confirmation",
            NotificationChannel.Email,
            new Dictionary<string, string>()));

        Assert.Equal("Template ativo não encontrado.", exception.Message);
    }

    [Fact]
    public async Task RenderAsync_WhenVariableIsMalformed_ShouldFailControlled()
    {
        var templateRepository = new FakeNotificationTemplateRepository();
        var renderer = new SimpleTemplateRenderer(templateRepository);

        templateRepository.Store(new NotificationTemplate
        {
            TemplateKey = "auth.email-confirmation",
            Channel = NotificationChannel.Email,
            Subject = "Confirme seu e-mail",
            HtmlBody = "<p>Código {{confirmationCode.</p>",
            TextBody = "Código {{confirmationCode."
        });

        var exception = await Assert.ThrowsAsync<DomainException>(() => renderer.RenderAsync(
            "auth.email-confirmation",
            NotificationChannel.Email,
            new Dictionary<string, string>
            {
                ["confirmationCode"] = "123456"
            }));

        Assert.Equal("Template possui variável inválida.", exception.Message);
        Assert.DoesNotContain("123456", exception.Message);
    }

    private sealed class FakeNotificationTemplateRepository : INotificationTemplateRepository
    {
        /// <summary>
        /// Campo que armazena templates.
        /// </summary>
        private readonly List<NotificationTemplate> _templates = [];

        public Task<IReadOnlyCollection<NotificationTemplate>> ListActiveAsync()
        {
            return Task.FromResult<IReadOnlyCollection<NotificationTemplate>>(_templates);
        }

        public Task<NotificationTemplate?> GetActiveAsync(string templateKey, NotificationChannel channel)
        {
            var template = _templates.FirstOrDefault(current =>
                current.TemplateKey == templateKey &&
                current.Channel == channel);

            return Task.FromResult(template);
        }

        public void Store(NotificationTemplate template)
        {
            _templates.Add(template);
        }
    }
}
