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
        public async Task UpdateAsync_Should_Fail_When_Entity_Is_Already_Tracked_Without_Fix()
        {
            // This test is designed to reproduced the issue. 
            // Once fixed, we should update the assertion or the test logic to expect success.
            // But strict TDD says: write a failing test first.
            
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

            // 2. Simulate the scenario: Entity is loaded into Local cache (tracked)
            // We use a query that tracks.
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

            // 4. Attempt to UpdateAsync with the detached entity
            // This SHOULD FAIL if the repository doesn't handle local detach.
            // If the fix is NOT implemented, this throws InvalidOperationException.
            
            // NOTE: For the TDD step, I expect this to FAIL.
            // However, since I cannot easily "assert it fails then assert it passes" without modifying the test code twice,
            // I will write the test expecting SUCCESS, so it fails NOW (Red), and passes LATER (Green).

            var exception = await Record.ExceptionAsync(async () => await repo.UpdateAsync(detachedDraw));
            
            // If the bug exists, exception will be not null (InvalidOperationException)
            // So to match "Red State", we assert that it succeeds, which will fail.
            Assert.Null(exception); 
            
            // Verify update happened
            context.ChangeTracker.Clear(); // Clear tracking to reload fresh
            var reloaded = await context.DrawRecords.FindAsync(draw.Id);
            Assert.Equal("Updated Notes", reloaded?.Notes);
        }
    }
}
