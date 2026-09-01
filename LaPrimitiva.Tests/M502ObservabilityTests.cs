using LaPrimitiva.App.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LaPrimitiva.Tests;

public class M502ObservabilityTests
{
    [Fact]
    public async Task CorrelationMiddleware_UsesServerTraceIdentifierAndReturnsItToTheCaller()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "server-generated-reference" };
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(
            "server-generated-reference",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public void HealthResponses_DoNotExposeChecksExceptionsOrConnectionStrings()
    {
        var program = ReadRepoFile("LaPrimitiva.App/Program.cs");

        Assert.Contains("status = report.Status.ToString()", program);
        Assert.Contains("correlationId = context.TraceIdentifier", program);
        Assert.DoesNotContain("report.Entries", program);
        Assert.DoesNotContain("ConnectionStrings:DefaultConnection", program);
    }

    [Fact]
    public void SecureJsonFileLogger_WritesStructuredExceptionAndCorrelationScope()
    {
        var directory = Path.Combine(Path.GetTempPath(), "laprimitiva-m502-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = new SecureJsonFileLoggerProvider(directory);
            var logger = provider.CreateLogger("M502Test");
            using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = "test-reference" }))
            {
                logger.LogError(new InvalidOperationException("technical detail"), "Failed {Operation}", "Verifier");
            }

            var logPath = Assert.Single(Directory.GetFiles(directory, "application-*.jsonl"));
            using var entry = JsonDocument.Parse(File.ReadAllText(logPath));
            Assert.Equal("Error", entry.RootElement.GetProperty("level").GetString());
            Assert.Equal("Verifier", entry.RootElement.GetProperty("properties").GetProperty("Operation").GetString());
            Assert.Contains("technical detail", entry.RootElement.GetProperty("exception").GetString());
            Assert.Equal(
                "test-reference",
                entry.RootElement.GetProperty("scopes")[0].GetProperty("CorrelationId").GetString());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SecureJsonFileLogger_NormalizesUnsupportedStructuredValuesWithoutThrowing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "laprimitiva-m601-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = new SecureJsonFileLoggerProvider(directory);
            var logger = provider.CreateLogger("M601Test");

            logger.LogInformation(
                "Endpoint metadata {Metadata}",
                (object)new object[] { typeof(M502ObservabilityTests) });

            var logPath = Assert.Single(Directory.GetFiles(directory, "application-*.jsonl"));
            using var entry = JsonDocument.Parse(File.ReadAllText(logPath));
            var metadata = entry.RootElement.GetProperty("properties").GetProperty("Metadata");
            Assert.Equal(JsonValueKind.Array, metadata.ValueKind);
            Assert.Equal(typeof(M502ObservabilityTests).FullName, metadata[0].GetString());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void UnexpectedUiErrors_AreLoggedButSafeMessagesDoNotUseProviderExceptionText()
    {
        foreach (var relativePath in new[]
        {
            "LaPrimitiva.App/Components/Pages/AutomatedCombination.razor",
            "LaPrimitiva.App/Components/Pages/Plans.razor",
            "LaPrimitiva.App/Components/Pages/Register.razor"
        })
        {
            var source = ReadRepoFile(relativePath);
            Assert.Contains("ErrorReporter.Report(ex", source);
        }

        var errorPage = ReadRepoFile("LaPrimitiva.App/Components/Pages/Error.razor");
        Assert.Contains("LG[\"ReferenceLabel\"]", errorPage);
        Assert.DoesNotContain("Exception", errorPage);
        Assert.DoesNotContain("Development Mode", errorPage);

        var rssNotification = ReadRepoFile("LaPrimitiva.Application/Services/DrawNotificationService.cs");
        Assert.DoesNotContain("LastError = $\"Error al sincronizar sorteos: {ex.Message}\"", rssNotification);
        Assert.Contains("errorReporter.Report(ex, \"RssImport\")", rssNotification);
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LaPrimitiva.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
