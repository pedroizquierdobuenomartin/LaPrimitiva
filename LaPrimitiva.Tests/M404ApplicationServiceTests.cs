using System.Linq.Expressions;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using Moq;

namespace LaPrimitiva.Tests;

public class M404ApplicationServiceTests
{
    [Fact]
    public async Task DashboardService_AppliesYearFilterAndBuildsSummaryAndMonthlySeries()
    {
        var repository = new Mock<IDrawRepository>();
        var source = new List<DrawRecord>
        {
            CreatePlayedDraw(new DateTime(2026, 2, 5), spent: 3m, won: 6m),
            CreatePlayedDraw(new DateTime(2025, 12, 20), spent: 2m, won: 0m)
        };
        repository
            .Setup(port => port.GetListAsync(It.IsAny<Expression<Func<DrawRecord, bool>>?>()))
            .ReturnsAsync((Expression<Func<DrawRecord, bool>>? predicate) =>
                predicate is null ? source : source.Where(predicate.Compile()).ToList());
        var service = new DashboardService(repository.Object, new SummaryService());

        var dashboard = await service.GetDashboardAsync(2026);

        Assert.Equal(3m, dashboard.Summary.TotalSpent);
        Assert.Equal(6m, dashboard.Summary.TotalWon);
        var month = Assert.Single(dashboard.MonthlySummaries);
        Assert.Equal(2026, month.Year);
        Assert.Equal(2, month.Month);
    }

    [Fact]
    public async Task DashboardService_WithoutYearRequestsCompleteHistory()
    {
        var repository = new Mock<IDrawRepository>();
        repository.Setup(port => port.GetListAsync(null)).ReturnsAsync([]);
        var service = new DashboardService(repository.Object, new SummaryService());

        await service.GetDashboardAsync();

        repository.Verify(port => port.GetListAsync(null), Times.Once);
    }

    [Fact]
    public async Task DataExportService_ReturnsEveryDrawOrderedChronologically()
    {
        var firstPlanId = Guid.NewGuid();
        var secondPlanId = Guid.NewGuid();
        var repository = new Mock<IDrawRepository>();
        repository.Setup(port => port.GetListAsync(null)).ReturnsAsync(
        [
            new DrawRecord { PlanId = firstPlanId, DrawDate = new DateTime(2026, 1, 8), Neto = -2m, Acumulado = 999m },
            new DrawRecord { PlanId = secondPlanId, DrawDate = new DateTime(2026, 1, 7), Neto = 4m, Acumulado = 999m },
            new DrawRecord { PlanId = firstPlanId, DrawDate = new DateTime(2026, 1, 5), Neto = 5m, Acumulado = 999m }
        ]);
        var service = new DataExportService(repository.Object);

        var result = await service.GetAllDrawsAsync();

        Assert.Equal(
            new[] { new DateTime(2026, 1, 5), new DateTime(2026, 1, 7), new DateTime(2026, 1, 8) },
            result.Select(draw => draw.DrawDate));
        Assert.Equal(new[] { 5m, 4m, 3m }, result.Select(draw => draw.Acumulado));
        repository.Verify(port => port.GetListAsync(null), Times.Once);
    }

    private static DrawRecord CreatePlayedDraw(DateTime date, decimal spent, decimal won)
    {
        var draw = new DrawRecord
        {
            DrawDate = date,
            Played = true,
            CosteFija = spent,
            FixedPrize = won
        };
        draw.RecalculateFinancials();
        return draw;
    }
}
