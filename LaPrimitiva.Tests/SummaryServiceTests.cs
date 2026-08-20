using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Tests;

public class SummaryServiceTests
{
    [Fact]
    public void GetSummary_UsesUnifiedTotalsForDashboardNetAndRoi()
    {
        var draw = new DrawRecord
        {
            Played = true,
            CosteFija = 1m,
            CosteAuto = 1m,
            CosteJokerFija = 0.5m,
            CosteJokerAuto = 0.5m,
            FixedPrize = 2m,
            JokerFixedPrize = 4m
        };
        draw.RecalculateFinancials();

        var summary = new SummaryService().GetSummary([draw]);

        Assert.Equal(3m, summary.TotalSpent);
        Assert.Equal(6m, summary.TotalWon);
        Assert.Equal(3m, summary.NetResult);
        Assert.Equal(100m, summary.ROI);
        Assert.Equal(summary.FixedSpent + summary.AutoSpent + summary.JokerFixedSpent + summary.JokerAutoSpent, summary.TotalSpent);
        Assert.Equal(summary.FixedWon + summary.AutoWon + summary.JokerFixedWon + summary.JokerAutoWon, summary.TotalWon);
    }
}
