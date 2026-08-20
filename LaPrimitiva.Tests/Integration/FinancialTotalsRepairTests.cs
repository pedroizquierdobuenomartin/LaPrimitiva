using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using LaPrimitiva.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LaPrimitiva.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public class FinancialTotalsRepairTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task SeedStartup_RepairsExistingTotalsThatExcludeJoker()
    {
        await ResetDatabaseAsync();

        var planId = Guid.NewGuid();
        var drawId = Guid.NewGuid();
        using (var arrangeScope = CreateScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
            context.Plans.Add(new Plan
            {
                Id = planId,
                Name = "Plan Joker",
                EffectiveFrom = new DateTime(2026, 1, 1),
                CostPerBet = 1m,
                EnableJoker = true,
                JokerCostPerBet = 0.5m
            });
            context.DrawRecords.Add(new DrawRecord
            {
                Id = drawId,
                PlanId = planId,
                DrawDate = new DateTime(2026, 8, 20),
                DrawType = DrawType.Jueves,
                WeekNumber = 34,
                Played = true,
                CosteFija = 1m,
                CosteAuto = 1m,
                CosteJokerFija = 0.5m,
                CosteJokerAuto = 0.5m,
                FixedPrize = 2m,
                AutoPrize = 3m,
                JokerFixedPrize = 10m,
                JokerAutoPrize = 20m,
                TotalCoste = 2m,
                TotalPremios = 5m,
                Neto = 3m
            });
            await context.SaveChangesAsync();
        }

        using (var repairScope = CreateScope())
        {
            var seeder = repairScope.ServiceProvider.GetRequiredService<WinningDrawSeeder>();
            await seeder.SeedFromDirectoryAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        }

        using var assertScope = CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
        var repaired = await assertContext.DrawRecords.AsNoTracking().SingleAsync(draw => draw.Id == drawId);
        Assert.Equal(3m, repaired.TotalCoste);
        Assert.Equal(35m, repaired.TotalPremios);
        Assert.Equal(32m, repaired.Neto);
    }
}
