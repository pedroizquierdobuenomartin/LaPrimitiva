using Microsoft.EntityFrameworkCore;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using Xunit;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LaPrimitiva.Tests
{
    public class PlanServiceTests
    {
        private PrimitivaDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PrimitivaDbContext(options);
        }

        [Fact]
        public async Task CreatePlanAsync_ShouldThrowException_WhenDatesOverlap()
        {
            // Arrange
            var context = GetDbContext();
            var service = new PlanService(context);

            var existingPlan = new Plan
            {
                Name = "Existing",
                EffectiveFrom = new DateTime(2026, 1, 1),
                EffectiveTo = new DateTime(2026, 12, 31)
            };
            context.Plans.Add(existingPlan);
            await context.SaveChangesAsync();

            var overlappingPlan = new Plan
            {
                Name = "Overlapping",
                EffectiveFrom = new DateTime(2026, 6, 1),
                EffectiveTo = new DateTime(2027, 5, 31)
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePlanAsync(overlappingPlan));
        }

        [Fact]
        public async Task UpdatePlanAsync_ShouldThrowException_WhenDatesOverlapWithOtherPlan()
        {
            // Arrange
            var context = GetDbContext();
            var service = new PlanService(context);

            var otherPlan = new Plan
            {
                Name = "Other",
                EffectiveFrom = new DateTime(2026, 1, 1),
                EffectiveTo = new DateTime(2026, 6, 30)
            };
            var myPlan = new Plan
            {
                Name = "Mine",
                EffectiveFrom = new DateTime(2026, 7, 1),
                EffectiveTo = new DateTime(2026, 12, 31)
            };
            context.Plans.AddRange(otherPlan, myPlan);
            await context.SaveChangesAsync();

            // Act: Update myPlan to overlap with otherPlan
            myPlan.EffectiveFrom = new DateTime(2026, 6, 1);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePlanAsync(myPlan));
        }

        [Fact]
        public async Task DeletePlanAsync_ShouldThrowException_WhenPlanHasDraws()
        {
            // Arrange
            var context = GetDbContext();
            var service = new PlanService(context);

            var plan = new Plan { Name = "Plan with Draws" };
            var draw = new DrawRecord { PlanId = plan.Id, DrawDate = DateTime.Today };
            context.Plans.Add(plan);
            context.DrawRecords.Add(draw);
            await context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePlanAsync(plan.Id));
        }
    }
}
