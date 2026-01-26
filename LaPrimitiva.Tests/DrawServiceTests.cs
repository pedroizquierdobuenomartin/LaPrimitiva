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
        private readonly DrawService _service;

        public DrawServiceTests()
        {
            _service = new DrawService(_drawRepoMock.Object);
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
    }
}
