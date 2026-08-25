using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LaPrimitiva.Tests.Integration
{
    [Collection(IntegrationTestCollection.Name)]
    public class DrawRepositoryTrackingTests : IntegrationTestBase
    {
        public DrawRepositoryTrackingTests(IntegrationTestFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task UpdateAsync_UsesIndependentContext_WhenCircuitContextTracksSameEntity()
        {
            await ResetDatabaseAsync();

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
            var repo = scope.ServiceProvider.GetRequiredService<IDrawRepository>();

            // 1. Create a Plan and a DrawRecord
            var plan = new Plan 
            { 
                Name = "Test Plan", 
                EffectiveFrom = DateTime.UtcNow,
                EffectiveTo = DateTime.UtcNow.AddDays(7),
                BetsPerDraw = 10,
                CostPerBet = 1.0m
            };
            context.Plans.Add(plan);
            
            var draw = new DrawRecord
            {
                PlanId = plan.Id,
                Plan = plan,
                DrawDate = DateTime.UtcNow,
                WeekNumber = 1,
                Played = true
            };
            context.DrawRecords.Add(draw);
            await context.SaveChangesAsync();

            // Simulate a long-lived circuit context that already tracks the entity.
            var trackedDraw = await context.DrawRecords.FindAsync(draw.Id);
            Assert.NotNull(trackedDraw);
            
            // 3. Create a NEW instance with SAME ID (detached) simulating data from UI
            var detachedDraw = new DrawRecord
            {
                Id = draw.Id,
                PlanId = plan.Id,
                WeekNumber = 1,
                DrawDate = draw.DrawDate,
                Played = true,
                Notes = "Updated Notes",
                UpdatedAt = DateTime.UtcNow
            };

            var exception = await Record.ExceptionAsync(async () => await repo.UpdateAsync(detachedDraw));
            Assert.Null(exception);
            Assert.Same(trackedDraw, context.DrawRecords.Local.Single());

            // The repository's context was independent; reload to observe its committed update.
            context.ChangeTracker.Clear();
            var reloaded = await context.DrawRecords.FindAsync(draw.Id);
            Assert.Equal("Updated Notes", reloaded?.Notes);
        }
    }
}
