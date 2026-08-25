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
        public async Task CreatePlan_ShouldFail_WhenStartDateMatchesExistingEndDate()
        {
            await ResetDatabaseAsync();
            using var scope = CreateScope();
            var planService = scope.ServiceProvider.GetRequiredService<PlanService>();
            var context = scope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
            var existingPlan = new Plan
            {
                Name = "Existing Boundary Plan",
                EffectiveFrom = new DateTime(2032, 1, 1),
                EffectiveTo = new DateTime(2032, 12, 31),
                BetsPerDraw = 2
            };
            await planService.CreatePlanAsync(existingPlan);

            var matchingBoundaryPlan = new Plan
            {
                Name = "Matching Boundary Plan",
                EffectiveFrom = existingPlan.EffectiveTo!.Value,
                EffectiveTo = new DateTime(2033, 12, 30),
                BetsPerDraw = 2
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => planService.CreatePlanAsync(matchingBoundaryPlan));
            Assert.Equal(1, await context.Plans.CountAsync());
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
                BetsPerDraw = testPlan.BetsPerDraw,
                RowVersion = loadedPlanDto.RowVersion.ToArray()
            };

            // Act
            await planService.UpdatePlanAsync(updatedPlan);

            // Assert
            var result = await planService.GetPlanByIdAsync(testPlan.Id);
            Assert.Equal("Update Test Plan Updated", result?.Name);
        }

        [Fact]
        public async Task UpdatePlan_ShouldFail_WhenEndDateMatchesAnotherStartDate()
        {
            await ResetDatabaseAsync();
            using var scope = CreateScope();
            var planService = scope.ServiceProvider.GetRequiredService<PlanService>();
            var context = scope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
            var editablePlan = new Plan
            {
                Name = "Editable Boundary Plan",
                EffectiveFrom = new DateTime(2033, 1, 1),
                EffectiveTo = new DateTime(2033, 12, 31),
                BetsPerDraw = 2
            };
            var existingPlan = new Plan
            {
                Name = "Existing Next Plan",
                EffectiveFrom = new DateTime(2034, 1, 1),
                EffectiveTo = new DateTime(2034, 12, 31),
                BetsPerDraw = 2
            };
            await planService.CreatePlanAsync(editablePlan);
            await planService.CreatePlanAsync(existingPlan);

            var overlappingUpdate = new Plan
            {
                Id = editablePlan.Id,
                Name = editablePlan.Name,
                EffectiveFrom = editablePlan.EffectiveFrom,
                EffectiveTo = existingPlan.EffectiveFrom,
                CostPerBet = editablePlan.CostPerBet,
                BetsPerDraw = editablePlan.BetsPerDraw
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => planService.UpdatePlanAsync(overlappingUpdate));
            var persisted = await context.Plans
                .AsNoTracking()
                .SingleAsync(plan => plan.Id == editablePlan.Id);
            Assert.Equal(new DateTime(2033, 12, 31), persisted.EffectiveTo);
        }

        [Fact]
        public async Task UpdatePlan_ShouldPreserveCreatedAt_AndRefreshUpdatedAt()
        {
            // Arrange
            await ResetDatabaseAsync();
            using var scope = CreateScope();
            var planService = scope.ServiceProvider.GetRequiredService<PlanService>();
            var context = scope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
            var originalCreatedAt = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var originalUpdatedAt = new DateTime(2020, 1, 3, 3, 4, 5, DateTimeKind.Utc);
            var testPlan = new Plan
            {
                Name = "Timestamp Test Plan",
                EffectiveFrom = new DateTime(2031, 1, 1),
                EffectiveTo = new DateTime(2031, 12, 31),
                CostPerBet = 1.0m,
                BetsPerDraw = 2,
                CreatedAt = originalCreatedAt,
                UpdatedAt = originalUpdatedAt
            };
            await planService.CreatePlanAsync(testPlan);

            var loadedPlanDto = await planService.GetPlanByIdAsync(testPlan.Id);
            Assert.NotNull(loadedPlanDto);

            var disconnectedPlan = new Plan
            {
                Id = testPlan.Id,
                Name = "Timestamp Test Plan Updated",
                EffectiveFrom = testPlan.EffectiveFrom,
                EffectiveTo = testPlan.EffectiveTo,
                CostPerBet = testPlan.CostPerBet,
                BetsPerDraw = testPlan.BetsPerDraw,
                CreatedAt = originalCreatedAt.AddYears(10),
                UpdatedAt = originalUpdatedAt,
                RowVersion = loadedPlanDto.RowVersion.ToArray()
            };

            // Act
            await planService.UpdatePlanAsync(disconnectedPlan);

            // Assert
            var persisted = await context.Plans
                .AsNoTracking()
                .SingleAsync(plan => plan.Id == testPlan.Id);
            Assert.Equal("Timestamp Test Plan Updated", persisted.Name);
            Assert.Equal(originalCreatedAt, persisted.CreatedAt);
            Assert.True(persisted.UpdatedAt > originalUpdatedAt);
        }
    }
}
