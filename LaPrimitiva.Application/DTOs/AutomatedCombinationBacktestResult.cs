namespace LaPrimitiva.Application.DTOs;

public record AutomatedCombinationBacktestResult
{
    public int HistoricalDraws { get; init; }
    public int MinimumTrainingDraws { get; init; }
    public int EvaluatedDraws { get; init; }
    public DateTime? FirstEvaluatedDate { get; init; }
    public DateTime? LastEvaluatedDate { get; init; }
    public PredictionBacktestMetrics WeightedModel { get; init; } = new();
    public PredictionBacktestMetrics UniformBaseline { get; init; } = new();
    public double TheoreticalUniformAverageMatches { get; init; }
    public double ApproximateAverageZScore { get; init; }
    public bool HasConventionalStatisticalAdvantage { get; init; }
    public bool FixedCombinationAvailable { get; init; }
    public List<string> Limitations { get; init; } = [];
    public List<PredictionBacktestCase> Cases { get; init; } = [];
}

public record PredictionBacktestMetrics
{
    public int TotalMatches { get; init; }
    public double AverageMatches { get; init; }
    public int MaximumMatches { get; init; }
    public int DrawsWithAtLeastThreeMatches { get; init; }
    public Dictionary<int, int> MatchDistribution { get; init; } =
        Enumerable.Range(0, 7).ToDictionary(matches => matches, _ => 0);
}

public record PredictionBacktestCase
{
    public DateTime DrawDate { get; init; }
    public int TrainingDraws { get; init; }
    public List<int> ActualNumbers { get; init; } = [];
    public List<int> WeightedNumbers { get; init; } = [];
    public int WeightedMatches { get; init; }
    public List<int> UniformNumbers { get; init; } = [];
    public int UniformMatches { get; init; }
}
