using System.Text.RegularExpressions;

namespace NotificationCore.IntegrationTests.Observability;

public sealed class ObservabilityArchitectureTests
{
    [Fact]
    public void Application_WhenInspected_ShouldNotContainTelemetryAbstractions()
    {
        var root = FindRepositoryRoot();
        var applicationPath = Path.Combine(root, "src", "Backend", "NotificationCore", "NotificationCore.Application");
        var applicationFiles = EnumerateSourceFiles(applicationPath);
        var applicationText = ReadAll(applicationFiles);
        var dispatcherText = File.ReadAllText(Path.Combine(
            applicationPath,
            "UseCases",
            "Notifications",
            "DispatchPendingNotification",
            "PendingNotificationDispatcher.cs"));

        Assert.DoesNotContain("OpenTelemetry", applicationText, StringComparison.Ordinal);
        Assert.DoesNotContain("ActivitySource", applicationText, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Metrics", applicationText, StringComparison.Ordinal);
        Assert.DoesNotContain("TraceParent", applicationText, StringComparison.Ordinal);
        Assert.DoesNotContain("TraceState", applicationText, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"interface\s+I\w*Telemetry\b"), applicationText);
        Assert.DoesNotContain("Telemetry", dispatcherText, StringComparison.Ordinal);
    }

    [Fact]
    public void Domain_WhenInspected_ShouldNotContainObservabilityAbstractions()
    {
        var root = FindRepositoryRoot();
        var domainPath = Path.Combine(root, "src", "Backend", "NotificationCore", "NotificationCore.Domain");
        var domainText = ReadAll(EnumerateSourceFiles(domainPath));

        Assert.DoesNotContain("OpenTelemetry", domainText, StringComparison.Ordinal);
        Assert.DoesNotContain("ActivitySource", domainText, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Metrics", domainText, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"interface\s+I\w*(Telemetry|Observability)\b"), domainText);
    }

    [Fact]
    public void ApiAndInfrastructure_WhenInspected_ShouldKeepTelemetryAtTechnicalBoundaries()
    {
        var root = FindRepositoryRoot();
        var notificationApiPath = Path.Combine(root, "src", "Backend", "NotificationCore", "NotificationCore.Api");
        var notificationInfrastructurePath = Path.Combine(root, "src", "Backend", "NotificationCore", "NotificationCore.Infrastructure");
        var authCorePath = Path.Combine(root, "src", "Backend", "AuthCore");
        var gatewayPath = Path.Combine(root, "src", "Backend", "Gateway");
        var apiText = ReadAll(EnumerateSourceFiles(notificationApiPath));
        var infrastructureText = ReadAll(EnumerateSourceFiles(notificationInfrastructurePath));
        var authCoreText = ReadAll(EnumerateSourceFiles(authCorePath));
        var gatewayText = ReadAll(EnumerateSourceFiles(gatewayPath));

        Assert.Contains("NotificationDispatchTelemetry.ActivitySourceName", apiText, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<NotificationDispatchTelemetry>", apiText, StringComparison.Ordinal);
        Assert.Contains("NotificationDispatchTelemetryContextReader", apiText, StringComparison.Ordinal);
        Assert.Contains("SmtpTelemetry.ActivitySourceName", apiText, StringComparison.Ordinal);
        Assert.Contains("SmtpTelemetry.MeterName", apiText, StringComparison.Ordinal);
        Assert.Contains("SmtpTelemetry.StartSendActivity", infrastructureText, StringComparison.Ordinal);
        Assert.Contains("SmtpTelemetry.CompleteSend", infrastructureText, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpTelemetry", authCoreText, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpTelemetry", gatewayText, StringComparison.Ordinal);
    }

    [Fact]
    public void Observability_WhenInspected_ShouldNotCreateAdditionalExporters()
    {
        var root = FindRepositoryRoot();
        var notificationApiPath = Path.Combine(root, "src", "Backend", "NotificationCore", "NotificationCore.Api");
        var notificationInfrastructurePath = Path.Combine(root, "src", "Backend", "NotificationCore", "NotificationCore.Infrastructure");
        var apiAndInfrastructureText = ReadAll(
            EnumerateSourceFiles(notificationApiPath).Concat(EnumerateSourceFiles(notificationInfrastructurePath)));

        Assert.DoesNotContain("AddConsoleExporter", apiAndInfrastructureText, StringComparison.Ordinal);
        Assert.DoesNotContain("AddOtlpExporter", apiAndInfrastructureText, StringComparison.Ordinal);
        Assert.DoesNotContain("TracerProviderBuilder", apiAndInfrastructureText, StringComparison.Ordinal);
        Assert.DoesNotContain("MeterProviderBuilder", apiAndInfrastructureText, StringComparison.Ordinal);
    }

    private static IReadOnlyCollection<string> EnumerateSourceFiles(string path)
    {
        return Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();
    }

    private static string ReadAll(IEnumerable<string> files)
    {
        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuthCore.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
