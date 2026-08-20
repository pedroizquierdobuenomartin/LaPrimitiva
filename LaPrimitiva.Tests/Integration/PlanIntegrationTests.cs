using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Application.DTOs;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.App;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaPrimitiva.Tests.Integration
{
    [Collection(IntegrationTestCollection.Name)]
    public class PlanIntegrationTests : IntegrationTestBase
    {
        public PlanIntegrationTests(IntegrationTestFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task GetPlansByYearAsync_ShouldReturnPlans_FromService()
        {
            // Arrange
            await ResetDatabaseAsync();
            using var scope = CreateScope();
            var planService = scope.ServiceProvider.GetRequiredService<PlanService>();

            var year = 2027;
            var testPlan = new Plan 
            { 
                Name = "Integration Test Plan", 
                EffectiveFrom = new DateTime(year, 1, 1),
                EffectiveTo = new DateTime(year, 12, 31),
                CostPerBet = 1.0m,
                BetsPerDraw = 2,
                EnableJoker = false,
                JokerCostPerBet = 0m
            };
            
            // Use PlanService to create, ensuring validations are run
            await planService.CreatePlanAsync(testPlan);

            // Act
            var results = await planService.GetPlansByYearAsync(year);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, p => p.Name == "Integration Test Plan");
        }

        [Fact]
        public async Task CreatePlan_ShouldFail_WhenDatesOverlap()
        {
            // Arrange
            await ResetDatabaseAsync();
            using var scope = CreateScope();
            var planService = scope.ServiceProvider.GetRequiredService<PlanService>();

            var existingPlan = new Plan
            {
                Name = "Existing Plan",
                EffectiveFrom = new DateTime(2025, 1, 1),
                EffectiveTo = new DateTime(2025, 12, 31),
                CostPerBet = 1.0m,
                BetsPerDraw = 2
            };
            await planService.CreatePlanAsync(existingPlan);

            var overlappingPlan = new Plan
            {
                Name = "Overlapping Plan",
                EffectiveFrom = new DateTime(2025, 6, 1),
                EffectiveTo = new DateTime(2026, 6, 1),
                CostPerBet = 1.0m,
                BetsPerDraw = 2
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => planService.CreatePlanAsync(overlappingPlan));
        }

        [Fact]
        public async Task Repository_ShouldRejectInvalidPlan_WhenApplicationServiceIsBypassed()
        {
            await ResetDatabaseAsync();
            using var scope = CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<LaPrimitiva.Domain.Repositories.IPlanRepository>();
            var invalidPlan = new Plan
            {
                Name = "Plan inválido",
                EffectiveFrom = new DateTime(2026, 12, 31),
                EffectiveTo = new DateTime(2026, 1, 1),
                BetsPerDraw = 2
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(invalidPlan));
        }

        [Fact]
        public async Task SqlConstraint_ShouldRejectInvalidPlan_WhenEveryServiceIsBypassed()
        {
            await ResetDatabaseAsync();
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
            context.Plans.Add(new Plan
            {
                Name = "SQL inválido",
                EffectiveFrom = new DateTime(2026, 12, 31),
                EffectiveTo = new DateTime(2026, 1, 1),
                CostPerBet = -1m,
                BetsPerDraw = 0
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task SqlTrigger_ShouldRejectOverlappingPeriods_WhenEveryServiceIsBypassed()
        {
            await ResetDatabaseAsync();
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
            context.Plans.Add(new Plan
            {
                Name = "Primero",
                EffectiveFrom = new DateTime(2026, 1, 1),
                EffectiveTo = new DateTime(2026, 12, 31),
                BetsPerDraw = 2
            });
            await context.SaveChangesAsync();

            context.Plans.Add(new Plan
            {
                Name = "Solapado",
                EffectiveFrom = new DateTime(2026, 6, 1),
                EffectiveTo = new DateTime(2027, 5, 31),
                BetsPerDraw = 2
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task UpdatePlan_ShouldSucceed_WhenSqlOverlapTriggerIsEnabled()
        {
            // Arrange
            await ResetDatabaseAsync();
            using var scope = CreateScope();
            var planService = scope.ServiceProvider.GetRequiredService<PlanService>();

            var year = 2030;
            var testPlan = new Plan 
            { 
                Name = "Update Test Plan", 
                EffectiveFrom = new DateTime(year, 1, 1),
                EffectiveTo = new DateTime(year, 12, 31),
                CostPerBet = 1.0m,
                BetsPerDraw = 2
            };
            await planService.CreatePlanAsync(testPlan);

            // Fetch and update
            var loadedPlanDto = await planService.GetPlanByIdAsync(testPlan.Id);
            Assert.NotNull(loadedPlanDto);

            var updatedPlan = new Plan
            {
                Id = testPlan.Id,
                Name = "Update Test Plan Updated",
                EffectiveFrom = testPlan.EffectiveFrom,
                EffectiveTo = testPlan.EffectiveTo,
                CostPerBet = testPlan.CostPerBet,
                BetsPerDraw = testPlan.BetsPerDraw
            };

            // Act
            await planService.UpdatePlanAsync(updatedPlan);

            // Assert
            var result = await planService.GetPlanByIdAsync(testPlan.Id);
            Assert.Equal("Update Test Plan Updated", result?.Name);
        }
    }
}
