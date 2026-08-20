using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaPrimitiva.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public class DisconnectedDrawPersistenceTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task UpdateAsync_PersistsEditableValuesWithoutChangingStructuralColumns()
    {
        await ResetDatabaseAsync();

        var originalPlanId = Guid.NewGuid();
        var otherPlanId = Guid.NewGuid();
        var drawId = Guid.NewGuid();
        var originalDate = new DateTime(2026, 8, 20);
        var originalCreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        using (var arrangeScope = CreateScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
            context.Plans.AddRange(
                new Plan
                {
                    Id = originalPlanId,
                    Name = "Plan original",
                    EffectiveFrom = new DateTime(2026, 1, 1),
                    EffectiveTo = new DateTime(2026, 12, 31),
                    BetsPerDraw = 1,
                    CostPerBet = 1m
                },
                new Plan
                {
                    Id = otherPlanId,
                    Name = "Plan que no debe aplicarse",
                    EffectiveFrom = new DateTime(2026, 1, 1),
                    EffectiveTo = new DateTime(2026, 12, 31),
                    BetsPerDraw = 1,
                    CostPerBet = 2m
                });
            context.DrawRecords.Add(new DrawRecord
            {
                Id = drawId,
                PlanId = originalPlanId,
                DrawType = DrawType.Jueves,
                DrawDate = originalDate,
                WeekNumber = 34,
                Played = false,
                CreatedAt = originalCreatedAt,
                UpdatedAt = originalCreatedAt
            });
            await context.SaveChangesAsync();
        }

        var updatedAt = originalCreatedAt.AddDays(1);
        using (var updateScope = CreateScope())
        {
            var repository = updateScope.ServiceProvider.GetRequiredService<IDrawRepository>();
            var disconnected = Assert.Single(await repository.GetListAsync(draw => draw.Id == drawId));

            disconnected.Played = true;
            disconnected.CosteFija = 1m;
            disconnected.CosteAuto = 2m;
            disconnected.CosteJokerFija = 3m;
            disconnected.CosteJokerAuto = 4m;
            disconnected.FixedPrize = 10m;
            disconnected.AutoPrize = 20m;
            disconnected.JokerFixedPrize = 30m;
            disconnected.JokerAutoPrize = 40m;
            disconnected.TotalCoste = 3m; // Deliberately inconsistent: repository must enforce the domain invariant.
            disconnected.TotalPremios = 30m;
            disconnected.Neto = 27m;
            disconnected.Notes = "Actualizado desde una consulta sin seguimiento";
            disconnected.UpdatedAt = updatedAt;

            disconnected.PlanId = otherPlanId;
            disconnected.DrawType = DrawType.Sabado;
            disconnected.DrawDate = originalDate.AddDays(2);
            disconnected.WeekNumber = 99;
            disconnected.CreatedAt = originalCreatedAt.AddYears(1);

            await repository.UpdateAsync(disconnected);
        }

        using var assertScope = CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
        var persisted = await assertContext.DrawRecords
            .AsNoTracking()
            .SingleAsync(draw => draw.Id == drawId);

        Assert.True(persisted.Played);
        Assert.Equal(1m, persisted.CosteFija);
        Assert.Equal(2m, persisted.CosteAuto);
        Assert.Equal(3m, persisted.CosteJokerFija);
        Assert.Equal(4m, persisted.CosteJokerAuto);
        Assert.Equal(10m, persisted.FixedPrize);
        Assert.Equal(20m, persisted.AutoPrize);
        Assert.Equal(30m, persisted.JokerFixedPrize);
        Assert.Equal(40m, persisted.JokerAutoPrize);
        Assert.Equal(10m, persisted.TotalCoste);
        Assert.Equal(100m, persisted.TotalPremios);
        Assert.Equal(90m, persisted.Neto);
        Assert.Equal("Actualizado desde una consulta sin seguimiento", persisted.Notes);
        Assert.Equal(updatedAt, persisted.UpdatedAt);

        Assert.Equal(originalPlanId, persisted.PlanId);
        Assert.Equal(DrawType.Jueves, persisted.DrawType);
        Assert.Equal(originalDate, persisted.DrawDate);
        Assert.Equal(34, persisted.WeekNumber);
        Assert.Equal(originalCreatedAt, persisted.CreatedAt);
    }
}
