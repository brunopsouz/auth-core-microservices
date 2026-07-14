namespace Gateway.IntegrationTests;

public sealed class HealthCheckArchitectureTests
{
    private static readonly string[] BusinessLayerDirectories =
    [
        Path.Combine("src", "Backend", "AuthCore", "AuthCore.Application"),
        Path.Combine("src", "Backend", "AuthCore", "AuthCore.Domain"),
        Path.Combine("src", "Backend", "NotificationCore", "NotificationCore.Application"),
        Path.Combine("src", "Backend", "NotificationCore", "NotificationCore.Domain")
    ];

    [Fact]
    public void BusinessLayers_ShouldNotReferenceHealthChecks()
    {
        var repositoryRoot = GetRepositoryRoot();
        var forbiddenTerms = new[]
        {
            "Microsoft.Extensions.Diagnostics.HealthChecks",
            "IHealthCheck",
            "HealthCheckResult",
            "HealthStatus",
            "AddHealthChecks",
            "MapHealthChecks"
        };

        foreach (var directory in BusinessLayerDirectories)
        {
            var fullPath = Path.Combine(repositoryRoot, directory);
            var contents = Directory.EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText);

            foreach (var content in contents)
            {
                foreach (var forbiddenTerm in forbiddenTerms)
                    Assert.DoesNotContain(forbiddenTerm, content, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void SharedObservability_ShouldRemainDependencyAgnostic()
    {
        var repositoryRoot = GetRepositoryRoot();
        var sharedPath = Path.Combine(repositoryRoot, "src", "Shared", "Observability");
        var contents = Directory.EnumerateFiles(sharedPath, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText);
        var forbiddenTerms = new[]
        {
            "Npgsql",
            "StackExchange.Redis",
            "RabbitMQ.Client",
            "MailKit",
            "AuthCore.",
            "NotificationCore."
        };

        foreach (var content in contents)
        {
            foreach (var forbiddenTerm in forbiddenTerms)
                Assert.DoesNotContain(forbiddenTerm, content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Projects_ShouldNotReferenceExternalHealthChecksUiPackages()
    {
        var repositoryRoot = GetRepositoryRoot();
        var projectFiles = Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories);
        var forbiddenTerms = new[]
        {
            "AspNetCore.HealthChecks",
            "HealthChecks.UI"
        };

        foreach (var projectFile in projectFiles)
        {
            var content = File.ReadAllText(projectFile);

            foreach (var forbiddenTerm in forbiddenTerms)
                Assert.DoesNotContain(forbiddenTerm, content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "AuthCore.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Não foi possível localizar a raiz do repositório.");
    }
}
