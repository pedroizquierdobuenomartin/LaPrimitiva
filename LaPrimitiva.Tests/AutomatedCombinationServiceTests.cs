using System.Linq.Expressions;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using Moq;

namespace LaPrimitiva.Tests;

public class AutomatedCombinationServiceTests
{
    private readonly Mock<IWinningDrawRepository> _repository = new();

    [Fact]
    public async Task GenerateCombinationAsync_WithSameVariation_IsDeterministic()
    {
        var service = new AutomatedCombinationService(_repository.Object);

        var first = await service.GenerateCombinationAsync(variation: 0);
        var second = await service.GenerateCombinationAsync(variation: 0);

        Assert.Equal(first.Numbers, second.Numbers);
        Assert.Equal(first.Reintegro, second.Reintegro);
        Assert.Equal(0, first.DebugInfo["variation"]);
        Assert.Equal("uniform_without_replacement", first.DebugInfo["strategy"]);
        _repository.Verify(
            r => r.GetListAsync(It.IsAny<Expression<Func<WinningDraw, bool>>?>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateCombinationAsync_WithAnotherVariation_ReturnsAnotherCandidate()
    {
        var service = new AutomatedCombinationService(_repository.Object);

        var first = await service.GenerateCombinationAsync(variation: 0);
        var regenerated = await service.GenerateCombinationAsync(variation: 1);

        Assert.NotEqual(string.Join(',', first.Numbers), string.Join(',', regenerated.Numbers));
        Assert.Equal(1, regenerated.DebugInfo["variation"]);
    }

    [Fact]
    public async Task GenerateCombinationAsync_ReturnsOneValidUniformTicket()
    {
        var service = new AutomatedCombinationService(_repository.Object);

        var result = await service.GenerateCombinationAsync();

        Assert.Equal(6, result.Numbers.Count);
        Assert.Equal(6, result.Numbers.Distinct().Count());
        Assert.All(result.Numbers, number => Assert.InRange(number, 1, 49));
        Assert.True(result.Numbers.SequenceEqual(result.Numbers.OrderBy(number => number)));
        Assert.InRange(result.Reintegro, 0, 9);
        Assert.Equal(13_983_816, result.DebugInfo["possible_combinations"]);
        Assert.False(result.DebugInfo.ContainsKey("half_life_days"));
        Assert.False(result.DebugInfo.ContainsKey("top10_by_model"));
    }

    [Fact]
    public async Task GenerateCombinationAsync_WithNegativeVariation_IsRejected()
    {
        var service = new AutomatedCombinationService(_repository.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GenerateCombinationAsync(variation: -1));
    }

    [Fact]
    public async Task BacktestAsync_UsesOnlyDrawsBeforeEachPrediction()
    {
        var original = CreateDraws(30);
        var changedFuture = CreateDraws(30);
        changedFuture[^1] = CreateDraw(changedFuture[^1].DrawDate, [44, 45, 46, 47, 48, 49]);

        _repository
            .SetupSequence(r => r.GetListAsync(It.IsAny<Expression<Func<WinningDraw, bool>>?>()))
            .ReturnsAsync(original)
            .ReturnsAsync(changedFuture);
        var service = new AutomatedCombinationService(_repository.Object);

        var firstRun = await service.BacktestAsync(minimumTrainingDraws: 10);
        var changedFutureRun = await service.BacktestAsync(minimumTrainingDraws: 10);

        Assert.Equal(20, firstRun.EvaluatedDraws);
        Assert.Equal(10, firstRun.Cases[0].TrainingDraws);
        Assert.Equal(firstRun.Cases[0].WeightedNumbers, changedFutureRun.Cases[0].WeightedNumbers);
        Assert.Equal(firstRun.Cases[0].UniformNumbers, changedFutureRun.Cases[0].UniformNumbers);
    }

    [Fact]
    public async Task BacktestAsync_ReportsComparableWeightedAndUniformMetrics()
    {
        _repository
            .Setup(r => r.GetListAsync(It.IsAny<Expression<Func<WinningDraw, bool>>?>()))
            .ReturnsAsync(CreateDraws(40));
        var service = new AutomatedCombinationService(_repository.Object);

        var result = await service.BacktestAsync(minimumTrainingDraws: 12);

        Assert.Equal(28, result.EvaluatedDraws);
        Assert.Equal(result.EvaluatedDraws, result.WeightedModel.MatchDistribution.Values.Sum());
        Assert.Equal(result.EvaluatedDraws, result.UniformBaseline.MatchDistribution.Values.Sum());
        Assert.Equal(36.0 / 49.0, result.TheoreticalUniformAverageMatches, precision: 10);
        Assert.Equal(
            Math.Abs(result.ApproximateAverageZScore) >= 1.96,
            result.HasConventionalStatisticalAdvantage);
        Assert.False(result.FixedCombinationAvailable);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("no guarda", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BacktestAsync_IgnoresCorruptHistoricalDraws()
    {
        var draws = CreateDraws(3);
        draws.Insert(1, CreateDraw(new DateTime(2020, 1, 3), [0, 2, 3, 4, 5, 6]));
        _repository
            .Setup(r => r.GetListAsync(It.IsAny<Expression<Func<WinningDraw, bool>>?>()))
            .ReturnsAsync(draws);
        var service = new AutomatedCombinationService(_repository.Object);

        var result = await service.BacktestAsync(minimumTrainingDraws: 1);

        Assert.Equal(3, result.HistoricalDraws);
        Assert.Equal(2, result.EvaluatedDraws);
    }

    private static List<WinningDraw> CreateDraws(int count)
    {
        var start = new DateTime(2020, 1, 2);
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                var offset = index * 7 % 49;
                var numbers = Enumerable.Range(0, 6)
                    .Select(numberOffset => (offset + numberOffset) % 49 + 1)
                    .OrderBy(number => number)
                    .ToArray();

                return CreateDraw(start.AddDays(index * 3), numbers);
            })
            .ToList();
    }

    private static WinningDraw CreateDraw(DateTime date, IReadOnlyList<int> numbers) => new()
    {
        DrawDate = date,
        Number1 = numbers[0],
        Number2 = numbers[1],
        Number3 = numbers[2],
        Number4 = numbers[3],
        Number5 = numbers[4],
        Number6 = numbers[5],
        Complementario = Enumerable.Range(1, 49).First(number => !numbers.Contains(number)),
        Reintegro = 0
    };
}
