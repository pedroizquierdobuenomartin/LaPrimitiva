using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LaPrimitiva.Application.DTOs;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using Moq;
using Xunit;

namespace LaPrimitiva.Tests
{
    public class WinningDrawServiceTests
    {
        private readonly Mock<IWinningDrawRepository> _repositoryMock = new();
        private readonly WinningDrawService _service;

        public WinningDrawServiceTests()
        {
            _service = new WinningDrawService(_repositoryMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllDraws()
        {
            // Arrange
            var draws = new List<WinningDraw>
            {
                new() { Id = Guid.NewGuid(), DrawDate = DateTime.Now },
                new() { Id = Guid.NewGuid(), DrawDate = DateTime.Now.AddDays(-7) }
            };
            _repositoryMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<WinningDraw, bool>>>())).ReturnsAsync(draws);

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(draws[0].Id, result[0].Id); // Validamos mapeo y orden recibido
        }

        [Fact]
        public async Task CreateAsync_WhenDuplicateDate_ShouldReturnFailure()
        {
            // Arrange
            var dto = new WinningDrawDto(Guid.NewGuid(), DateTime.Now.Date, 1, 2, 3, 4, 5, 6, 7, 8, "J");
            _repositoryMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<WinningDraw, bool>>>())).ReturnsAsync(true);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("Ya existe un sorteo para la fecha especificada.", result.Error);
        }

        [Fact]
        public async Task CreateAsync_WhenValid_ShouldReturnSuccess()
        {
            // Arrange
            var dto = new WinningDrawDto(Guid.NewGuid(), DateTime.Now.Date, 1, 2, 3, 4, 5, 6, 7, 8, "J");
            _repositoryMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<WinningDraw, bool>>>())).ReturnsAsync(false);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            Assert.True(result.IsSuccess);
            _repositoryMock.Verify(r => r.CreateAsync(It.Is<WinningDraw>(d => d.DrawDate == dto.DrawDate)), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenValid_ShouldReturnSuccess()
        {
            // Arrange
            var dto = new WinningDrawDto(Guid.NewGuid(), DateTime.Now.Date, 1, 2, 3, 4, 5, 6, 7, 8, "J");
            _repositoryMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<WinningDraw, bool>>>())).ReturnsAsync(false);

            // Act
            var result = await _service.UpdateAsync(dto);

            // Assert
            Assert.True(result.IsSuccess);
            _repositoryMock.Verify(r => r.UpdateAsync(It.Is<WinningDraw>(d => d.Id == dto.Id)), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WhenDuplicateNumbers_ShouldReturnFailure()
        {
            // Arrange
            var dto = new WinningDrawDto(Guid.NewGuid(), DateTime.Now.Date, 3, 3, 3, 4, 5, 6, 7, 8, "J");

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("no se pueden repetir", result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldSortWinningNumbers()
        {
            // Arrange
            var dto = new WinningDrawDto(Guid.NewGuid(), DateTime.Now.Date, 45, 12, 33, 1, 9, 22, 10, 5, "J");
            _repositoryMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<WinningDraw, bool>>>())).ReturnsAsync(false);

            // Act
            await _service.CreateAsync(dto);

            // Assert
            _repositoryMock.Verify(r => r.CreateAsync(It.Is<WinningDraw>(d => 
                d.Number1 == 1 && 
                d.Number2 == 9 && 
                d.Number3 == 12 && 
                d.Number4 == 22 && 
                d.Number5 == 33 && 
                d.Number6 == 45)), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ShouldCallRepository()
        {
            // Arrange
            var id = Guid.NewGuid();

            // Act
            var result = await _service.DeleteAsync(id);

            // Assert
            Assert.True(result.IsSuccess);
            _repositoryMock.Verify(r => r.DeleteAsync(id), Times.Once);
        }
    }
}
