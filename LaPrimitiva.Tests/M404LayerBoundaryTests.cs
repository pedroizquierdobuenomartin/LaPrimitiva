using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Services;

namespace LaPrimitiva.Tests;

public class M404LayerBoundaryTests
{
    [Fact]
    public void ApplicationProject_DependsOnlyOnDomain()
    {
        var project = ReadRepoFile("LaPrimitiva.Application/LaPrimitiva.Application.csproj");

        Assert.Contains("LaPrimitiva.Domain", project);
        Assert.DoesNotContain("LaPrimitiva.Infrastructure", project);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", project);
    }

    [Fact]
    public void RazorComponents_DoNotAccessPersistenceOrRepositoriesDirectly()
    {
        var componentsRoot = Path.Combine(FindRepoRoot(), "LaPrimitiva.App", "Components");
        var components = Directory.GetFiles(componentsRoot, "*.razor", SearchOption.AllDirectories);

        foreach (var component in components)
        {
            var source = File.ReadAllText(component);
            Assert.DoesNotContain("PrimitivaDbContext", source);
            Assert.DoesNotContain("IDbContextFactory", source);
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source);
            Assert.DoesNotContain("LaPrimitiva.Infrastructure", source);
            Assert.DoesNotContain("Repository", source);
        }
    }

    [Fact]
    public void FinancialMetrics_UseOneRuleForNetRoiPercentageAndWinningBets()
    {
        var draw = new DrawRecord
        {
            FixedPrize = 5m,
            AutoPrize = 0m,
            JokerFixedPrize = 2m,
            JokerAutoPrize = 0m
        };

        Assert.Equal(7m, FinancialMetrics.CalculateNet(3m, 10m));
        Assert.Equal(100m, FinancialMetrics.CalculateRoi(3m, 6m));
        Assert.Equal(25d, FinancialMetrics.CalculatePercentage(1, 4));
        Assert.Equal(2, FinancialMetrics.CountWinningBets(draw));
        Assert.Equal(0m, FinancialMetrics.CalculateRoi(0m, 10m));
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
