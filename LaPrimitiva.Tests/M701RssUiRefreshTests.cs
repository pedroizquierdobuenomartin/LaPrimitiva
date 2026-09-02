namespace LaPrimitiva.Tests;

public class M701RssUiRefreshTests
{
    [Fact]
    public void HistoricalDraws_RefreshesItsDataAfterAnExternalSave_AndUnsubscribesOnDispose()
    {
        var source = ReadRepoFile("LaPrimitiva.App/Components/Pages/HistoricalDraws.razor");

        Assert.Contains("@implements IDisposable", source, StringComparison.Ordinal);
        Assert.Contains("@inject GlobalState GlobalState", source, StringComparison.Ordinal);
        Assert.Contains("GlobalState.OnDataRefreshRequired += HandleDataChange;", source, StringComparison.Ordinal);
        Assert.Contains("await LoadDraws(showLoader: false);", source, StringComparison.Ordinal);
        Assert.Contains("GlobalState.OnDataRefreshRequired -= HandleDataChange;", source, StringComparison.Ordinal);
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
