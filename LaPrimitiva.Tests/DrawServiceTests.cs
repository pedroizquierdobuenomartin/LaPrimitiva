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

        [Fact]
        public void CreateDrawTemplate_CentralizesCalendarAndDrawTypeRules()
        {
            var plan = new Plan { Id = Guid.NewGuid(), Name = "Plan 2026" };

            var draw = _service.CreateDrawTemplate(plan, 2026, 2, DayOfWeek.Thursday);

            Assert.Equal(new DateTime(2026, 1, 15), draw.DrawDate);
            Assert.Equal(2, draw.WeekNumber);
            Assert.Equal(DrawType.Jueves, draw.DrawType);
            Assert.Same(plan, draw.Plan);
        }

        [Fact]
        public async Task SaveDrawsAsync_CentralizesValidationAndPersistenceCoordination()
        {
            var plan = new Plan
            {
                Id = Guid.NewGuid(),
                Name = "Plan 2026",
                EffectiveFrom = new DateTime(2026, 1, 1),
                EffectiveTo = new DateTime(2026, 12, 31)
            };
            var newDraw = new DrawRecord
            {
                Id = Guid.Empty,
                PlanId = plan.Id,
                Plan = plan,
                DrawDate = new DateTime(2026, 1, 5)
            };
            var existingDraw = new DrawRecord
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                Plan = plan,
                DrawDate = new DateTime(2026, 1, 8)
            };
            _drawRepoMock
                .Setup(repository => repository.AnyAsync(It.IsAny<Expression<Func<DrawRecord, bool>>>()))
                .ReturnsAsync(false);
            _planRepoMock.Setup(repository => repository.GetAsync(plan.Id)).ReturnsAsync(plan);

            await _service.SaveDrawsAsync([newDraw, existingDraw]);

            _drawRepoMock.Verify(
                repository => repository.CreateRangeAsync(It.Is<IEnumerable<DrawRecord>>(draws => draws.Single() == newDraw)),
                Times.Once);
            _drawRepoMock.Verify(
                repository => repository.UpdateRangeAsync(It.Is<IEnumerable<DrawRecord>>(draws => draws.Single() == existingDraw)),
                Times.Once);
        }

        [Fact]
        public async Task GetDrawsByYearAsync_AppliesPlanSelectionAfterYearQuery()
        {
            var selectedPlanId = Guid.NewGuid();
            _drawRepoMock
                .Setup(repository => repository.GetListAsync(It.IsAny<Expression<Func<DrawRecord, bool>>?>()))
                .ReturnsAsync(
                [
                    new DrawRecord { PlanId = selectedPlanId, DrawDate = new DateTime(2026, 1, 5) },
                    new DrawRecord { PlanId = Guid.NewGuid(), DrawDate = new DateTime(2026, 1, 8) }
                ]);

            var result = await _service.GetDrawsByYearAsync(2026, selectedPlanId);

            Assert.Equal(selectedPlanId, Assert.Single(result).PlanId);
        }

        [Fact]
        public async Task GetDrawsForWeekAsync_AttachesPlanReturnedByPort()
        {
            var plan = new Plan { Id = Guid.NewGuid(), Name = "Plan 2026" };
            var draw = new DrawRecord
            {
                PlanId = plan.Id,
                DrawDate = new DateTime(2026, 1, 5),
                WeekNumber = 1
            };
            _drawRepoMock
                .Setup(repository => repository.GetListAsync(It.IsAny<Expression<Func<DrawRecord, bool>>?>()))
                .ReturnsAsync([draw]);
            _planRepoMock.Setup(repository => repository.GetByYearAsync(2026)).ReturnsAsync([plan]);

            var result = await _service.GetDrawsForWeekAsync(2026, 1);

            Assert.Same(plan, Assert.Single(result).Plan);
        }

        [Fact]
        public void GetCurrentWeekNumber_UsesFirstMondayAsWeekOneBoundary()
        {
            Assert.Equal(1, _service.GetCurrentWeekNumber(2026, new DateTime(2026, 1, 1)));
            Assert.Equal(1, _service.GetCurrentWeekNumber(2026, new DateTime(2026, 1, 5)));
            Assert.Equal(2, _service.GetCurrentWeekNumber(2026, new DateTime(2026, 1, 12)));
        }

        [Fact]
        public void CreateDrawTemplate_RejectsDayWithoutPrimitivaDraw()
        {
            var plan = new Plan { Id = Guid.NewGuid(), Name = "Plan 2026" };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.CreateDrawTemplate(plan, 2026, 1, DayOfWeek.Tuesday));
        }

        [Fact]
        public async Task SaveDrawAsync_RecalculatesTotalsBeforeCallingPort()
        {
            var draw = new DrawRecord
            {
                Played = true,
                CosteFija = 2m,
                CosteJokerFija = 1m,
                FixedPrize = 5m
            };

            await _service.SaveDrawAsync(draw);

            Assert.Equal(3m, draw.TotalCoste);
            Assert.Equal(5m, draw.TotalPremios);
            Assert.Equal(2m, draw.Neto);
            _drawRepoMock.Verify(repository => repository.UpdateAsync(draw), Times.Once);
        }
    }
}
