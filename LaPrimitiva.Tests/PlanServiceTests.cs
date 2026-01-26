using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using Moq;
using Xunit;

namespace LaPrimitiva.Tests
{
    /// <summary>
    /// Pruebas unitarias para PlanService utilizando mocks de repositorios.
    /// </summary>
    public class PlanServiceTests
    {
        private readonly Mock<IPlanRepository> _planRepoMock = new();
        private readonly Mock<IDrawRepository> _drawRepoMock = new();
        private readonly PlanService _service;

        public PlanServiceTests()
        {
            _service = new PlanService(_planRepoMock.Object, _drawRepoMock.Object);
        }

        [Fact]
        public async Task CreatePlanAsync_ShouldThrowException_WhenDatesOverlap()
        {
            // Arrange
            _planRepoMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Plan, bool>>>()))
                .ReturnsAsync(true);

            var overlappingPlan = new Plan
            {
                Name = "Overlapping",
                EffectiveFrom = new DateTime(2026, 6, 1),
                EffectiveTo = new DateTime(2027, 5, 31)
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreatePlanAsync(overlappingPlan));
        }

        [Fact]
        public async Task DeletePlanAsync_ShouldThrowException_WhenPlanHasDraws()
        {
            // Arrange
            var planId = Guid.NewGuid();
            _drawRepoMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<DrawRecord, bool>>>()))
                .ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeletePlanAsync(planId));
        }

        [Fact]
        public async Task GetPlansByYearAsync_ShouldReturnMappedDtos()
        {
            // Arrange
            var year = 2026;
            var plans = new List<Plan>
            {
                new Plan { Id = Guid.NewGuid(), Name = "Plan A", EffectiveFrom = new DateTime(year, 1, 1), Draws = new List<DrawRecord>() },
                new Plan { Id = Guid.NewGuid(), Name = "Plan B", EffectiveFrom = new DateTime(year, 7, 1), Draws = new List<DrawRecord> { new() } }
            };

            _planRepoMock.Setup(r => r.GetByYearAsync(year)).ReturnsAsync(plans);

            // Act
            var result = await _service.GetPlansByYearAsync(year);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.False(result[0].HasDraws);
            Assert.True(result[1].HasDraws);
        }
    }
}
