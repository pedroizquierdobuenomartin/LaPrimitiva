using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Exceptions;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LaPrimitiva.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public class M403ConcurrencyIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task UpdatingTwoCopies_RejectsTheStaleCopyWithoutOverwritingTheWinner()
    {
        var id = Guid.NewGuid();
        await using (var arrangeScope = _factory.Services.CreateAsyncScope())
        {
            var factory = arrangeScope.ServiceProvider.GetRequiredService<IDbContextFactory<PrimitivaDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            context.Plans.Add(new Plan
            {
                Id = id,
                Name = "Plan original",
                EffectiveFrom = new DateTime(2040, 1, 1),
                EffectiveTo = new DateTime(2040, 12, 31)
            });
            await context.SaveChangesAsync();
        }

        using var scope = CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPlanRepository>();
        var winner = await repository.GetAsync(id);
        var stale = await repository.GetAsync(id);
        Assert.NotNull(winner);
        Assert.NotNull(stale);

        winner!.Name = "Cambio ganador";
        stale!.Name = "Cambio obsoleto";
        await repository.UpdateAsync(winner);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.UpdateAsync(stale));

        var persisted = await repository.GetAsync(id);
        Assert.Equal("Cambio ganador", persisted!.Name);
    }
}
