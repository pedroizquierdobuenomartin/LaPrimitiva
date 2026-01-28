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
    /// Pruebas unitarias para DrawService utilizando mocks.
    /// </summary>
    public class DrawServiceTests
    {
        private readonly Mock<IDrawRepository> _drawRepoMock = new();
        private readonly Mock<IPlanRepository> _planRepoMock = new();
        private readonly DrawService _service;

        public DrawServiceTests()
        {
            _service = new DrawService(_drawRepoMock.Object, _planRepoMock.Object);
        }

        [Fact]
        public async Task DeleteWeeklyDrawAsync_CallsRepositoryWithCorrectFilter()
        {
            // Arrange
            var planId = Guid.NewGuid();
            var year = 2026;
            var week = 10;

            // Act
            await _service.DeleteWeeklyDrawAsync(week, year, planId);

            // Assert
            _drawRepoMock.Verify(r => r.DeleteRangeAsync(It.IsAny<Expression<Func<DrawRecord, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task ValidateDrawAsync_ShouldThrowException_WhenDateIsDuplicate()
        {
            // Arrange
            _drawRepoMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<DrawRecord, bool>>>()))
                .ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.ValidateDrawAsync(Guid.NewGuid(), DateTime.Now));
        }

        [Fact]
        public async Task ValidateDrawAsync_ShouldThrowException_WhenDateIsOutsidePlanRange()
        {
            // Arrange
            var planId = Guid.NewGuid();
            var plan = new Plan
            {
                Id = planId,
                EffectiveFrom = new DateTime(2026, 1, 1),
                EffectiveTo = new DateTime(2026, 12, 31)
            };

            _drawRepoMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<DrawRecord, bool>>>()))
                .ReturnsAsync(false);
            _planRepoMock.Setup(r => r.GetAsync(planId)).ReturnsAsync(plan);

            // Act & Assert (Date before)
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.ValidateDrawAsync(planId, new DateTime(2025, 12, 31)));

            // Act & Assert (Date after)
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.ValidateDrawAsync(planId, new DateTime(2027, 1, 1)));
        }
    }
}
