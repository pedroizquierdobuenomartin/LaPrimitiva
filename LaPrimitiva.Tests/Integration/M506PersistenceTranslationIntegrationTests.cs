using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Errors;
using LaPrimitiva.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LaPrimitiva.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class M506PersistenceTranslationIntegrationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task GlobalErrorPage_IsLocalizedAndDoesNotExposeTechnicalDetails()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/Error", TestContext.Current.CancellationToken);

        Assert.Contains("No hemos podido completar la operación", html);
        Assert.Contains("Referencia:", html);
        Assert.Contains("Volver al inicio", html);
        Assert.DoesNotContain("StackTrace", html);
        Assert.DoesNotContain("Development Mode", html);
    }

    [Fact]
    public async Task DuplicateWinningDrawDate_IsTranslatedFromSqlServerUniqueConstraint()
    {
        using var scope = CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWinningDrawRepository>();
        var date = new DateTime(2026, 8, 27);
        await repository.CreateAsync(CreateDraw(date));

        var exception = await Assert.ThrowsAsync<DataIntegrityException>(
            () => repository.CreateAsync(CreateDraw(date)));

        Assert.Equal("persistence.unique-constraint", exception.IntegrityCode);
        Assert.Equal(ErrorCategory.Integrity, exception.Error.Category);
    }

    private static WinningDraw CreateDraw(DateTime date) => new()
    {
        DrawDate = date,
        Number1 = 1,
        Number2 = 8,
        Number3 = 15,
        Number4 = 22,
        Number5 = 35,
        Number6 = 49,
        Complementario = 7,
        Reintegro = 0,
        Joker = "0123456"
    };
}
