using Xunit;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace LaPrimitiva.Tests
{
    public class DrawServiceTests
    {
        private PrimitivaDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PrimitivaDbContext(options);
        }

        [Fact]
        public async Task DeleteWeeklyDrawAsync_DeletesDrawsForSpecificWeekAndPlan()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var service = new DrawService(context);
            var planId = Guid.NewGuid();
            var year = 2026;
            
            // Data to be deleted
            var draw1 = new DrawRecord 
            { 
                PlanId = planId, 
                WeekNumber = 10, 
                DrawDate = new DateTime(year, 3, 2), // Monday
                DrawType = DrawType.Lunes 
            };
            var draw2 = new DrawRecord 
            { 
                PlanId = planId, 
                WeekNumber = 10, 
                DrawDate = new DateTime(year, 3, 5), // Thursday
                DrawType = DrawType.Jueves 
            };

            // Data to keep (Same plan, different week)
            var drawKeep1 = new DrawRecord 
            { 
                PlanId = planId, 
                WeekNumber = 11, 
                DrawDate = new DateTime(year, 3, 9), 
                DrawType = DrawType.Lunes 
            };

            // Data to keep (Different plan, same week - unlikely but good for isolation check)
            var otherPlanId = Guid.NewGuid();
            var drawKeep2 = new DrawRecord 
            { 
                PlanId = otherPlanId, 
                WeekNumber = 10, 
                DrawDate = new DateTime(year, 3, 2), 
                DrawType = DrawType.Lunes 
            };

            context.DrawRecords.AddRange(draw1, draw2, drawKeep1, drawKeep2);
            await context.SaveChangesAsync();

            // Act
            await service.DeleteWeeklyDrawAsync(10, year, planId);

            // Assert
            var remaining = await context.DrawRecords.ToListAsync();
            Assert.Equal(2, remaining.Count);
            Assert.Contains(remaining, d => d.Id == drawKeep1.Id);
            Assert.Contains(remaining, d => d.Id == drawKeep2.Id);
            Assert.DoesNotContain(remaining, d => d.Id == draw1.Id);
            Assert.DoesNotContain(remaining, d => d.Id == draw2.Id);
        }
    }
}
