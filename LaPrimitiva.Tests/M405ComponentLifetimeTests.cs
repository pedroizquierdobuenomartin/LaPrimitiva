using System.Text.RegularExpressions;

namespace LaPrimitiva.Tests;

public class M405ComponentLifetimeTests
{
    [Fact]
    public void Components_DoNotDeclareAsyncVoidHandlers()
    {
        var componentsRoot = Path.Combine(FindRepoRoot(), "LaPrimitiva.App", "Components");
        var components = Directory.GetFiles(componentsRoot, "*.razor", SearchOption.AllDirectories);

        foreach (var component in components)
        {
            var source = File.ReadAllText(component);
            Assert.DoesNotMatch(new Regex(@"\basync\s+void\b"), source);
        }
    }

    [Fact]
    public void MainLayout_DisposesTimerAndUnsubscribesEveryEvent()
    {
        var source = ReadRepoFile("LaPrimitiva.App/Components/Layout/MainLayout.razor");

        Assert.Contains("_feedbackTimer?.Dispose();", source);
        Assert.Contains("_feedbackTimer = null;", source);
        Assert.Contains("GlobalState.OnChange -= HandleStateChange;", source);
        Assert.Contains("NavigationManager.LocationChanged -= HandleLocationChanged;", source);
    }

    [Fact]
    public void Breadcrumb_UsesRemovableLocationChangedSubscription()
    {
        var source = ReadRepoFile("LaPrimitiva.App/Components/Layout/Breadcrumb.razor");

        Assert.Contains("@implements IDisposable", source);
        Assert.Contains("NavigationManager.LocationChanged += HandleLocationChanged;", source);
        Assert.Contains("NavigationManager.LocationChanged -= HandleLocationChanged;", source);
        Assert.DoesNotContain("LocationChanged += (", source);
    }

    [Theory]
    [InlineData("LaPrimitiva.App/Components/Layout/MainLayout.razor")]
    [InlineData("LaPrimitiva.App/Components/Pages/Home.razor")]
    [InlineData("LaPrimitiva.App/Components/Pages/Plans.razor")]
    [InlineData("LaPrimitiva.App/Components/Pages/Register.razor")]
    public void AsyncEventComponents_GuardQueuedCallbacksAfterDisposal(string relativePath)
    {
        var source = ReadRepoFile(relativePath);

        Assert.Contains("private bool _disposed;", source);
        Assert.Contains("if (_disposed)", source);
        Assert.Contains("_disposed = true;", source);
        Assert.Contains("Logger.LogError", source);
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
