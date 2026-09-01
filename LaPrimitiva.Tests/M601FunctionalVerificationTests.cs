namespace LaPrimitiva.Tests;

public class M601FunctionalVerificationTests
{
    [Fact]
    public void Register_NewWeekHeader_UsesTheSelectedPlanMetadata()
    {
        var source = File.ReadAllText(GetRepositoryFile("LaPrimitiva.App", "Components", "Pages", "Register.razor"));

        Assert.Contains("DisplayedModalPlan?.BetsPerDraw", source, StringComparison.Ordinal);
        Assert.Contains("DisplayedModalPlan?.EnableJoker", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_MobileRegistrationIcon_DoesNotContainInvalidArcFlags()
    {
        var source = File.ReadAllText(GetRepositoryFile("LaPrimitiva.App", "Components", "Layout", "MainLayout.razor"));

        Assert.DoesNotContain("a2 2 100 4", source, StringComparison.Ordinal);
    }

    private static string GetRepositoryFile(params string[] segments)
    {
        var path = AppContext.BaseDirectory;
        while (path is not null && !File.Exists(Path.Combine(path, "LaPrimitiva.sln")))
        {
            path = Directory.GetParent(path)?.FullName;
        }

        Assert.NotNull(path);
        return Path.Combine([path, .. segments]);
    }
}
