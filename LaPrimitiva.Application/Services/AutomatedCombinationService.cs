using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Application.DTOs;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Domain.Repositories;

namespace LaPrimitiva.Application.Services
{
    public class AutomatedCombinationService : IAutomatedCombinationService
    {
        private readonly IWinningDrawRepository _repository;

        public AutomatedCombinationService(IWinningDrawRepository repository)
        {
            _repository = repository;
        }

        public Task<CombinationResult> GenerateCombinationAsync(int variation = 0)
        {
            if (variation < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(variation), "La variación no puede ser negativa.");
            }

            var random = new Random(GetVariationSeed(DateTime.Today, variation));
            var pickedNumbers = GenerateUniformNumbers(random);
            var reintegro = PickReintegro(random);

            return Task.FromResult(new CombinationResult
            {
                Numbers = pickedNumbers,
                Reintegro = reintegro,
                DebugInfo = new Dictionary<string, object>
                {
                    { "strategy", "uniform_without_replacement" },
                    { "variation", variation },
                    { "possible_combinations", 13_983_816 }
                }
            });
        }

        public async Task<AutomatedCombinationBacktestResult> BacktestAsync(
            int minimumTrainingDraws = 104,
            double halfLifeDays = 365.0,
            double alpha = 1.0)
        {
            if (minimumTrainingDraws < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumTrainingDraws),
                    "El backtest necesita al menos un sorteo de entrenamiento.");
            }

            ValidateModelParameters(halfLifeDays, alpha);

            var draws = (await _repository.GetListAsync())
                .Where(draw => draw.IsValid())
                .OrderBy(draw => draw.DrawDate)
                .ToList();
            var cases = new List<PredictionBacktestCase>();

            for (var index = minimumTrainingDraws; index < draws.Count; index++)
            {
                var target = draws[index];
                var trainingDraws = draws.Take(index).ToList();
                var probabilities = CalculateWeightedProbabilities(
                    trainingDraws,
                    target.DrawDate.Date,
                    halfLifeDays,
                    alpha);

                var weightedNumbers = PickWeekly(
                    probabilities,
                    new Random(GetVariationSeed(target.DrawDate.Date, variation: 0)));
                var uniformNumbers = GenerateUniformNumbers(
                    new Random(GetUniformBaselineSeed(target.DrawDate.Date)));
                var actualNumbers = GetNumbers(target);

                cases.Add(new PredictionBacktestCase
                {
                    DrawDate = target.DrawDate,
                    TrainingDraws = trainingDraws.Count,
                    ActualNumbers = actualNumbers,
                    WeightedNumbers = weightedNumbers,
                    WeightedMatches = CountMatches(weightedNumbers, actualNumbers),
                    UniformNumbers = uniformNumbers,
                    UniformMatches = CountMatches(uniformNumbers, actualNumbers)
                });
            }

            var weightedMetrics = BuildMetrics(cases.Select(item => item.WeightedMatches));
            var uniformMetrics = BuildMetrics(cases.Select(item => item.UniformMatches));
            const double theoreticalUniformAverage = 36.0 / 49.0;
            const double uniformMatchVariance = 6.0 * (6.0 / 49.0) * (43.0 / 49.0) * (43.0 / 48.0);
            var approximateZScore = cases.Count == 0
                ? 0
                : (weightedMetrics.AverageMatches - theoreticalUniformAverage) /
                  Math.Sqrt(uniformMatchVariance / cases.Count);

            return new AutomatedCombinationBacktestResult
            {
                HistoricalDraws = draws.Count,
                MinimumTrainingDraws = minimumTrainingDraws,
                EvaluatedDraws = cases.Count,
                FirstEvaluatedDate = cases.FirstOrDefault()?.DrawDate,
                LastEvaluatedDate = cases.LastOrDefault()?.DrawDate,
                WeightedModel = weightedMetrics,
                UniformBaseline = uniformMetrics,
                TheoreticalUniformAverageMatches = theoreticalUniformAverage,
                ApproximateAverageZScore = approximateZScore,
                HasConventionalStatisticalAdvantage = Math.Abs(approximateZScore) >= 1.96,
                FixedCombinationAvailable = false,
                Limitations =
                [
                    "La aplicación no guarda los números de las apuestas fijas ni automáticas históricas; este resultado es una simulación walk-forward.",
                    "La línea base uniforme es determinista y sirve para reproducibilidad, no como estimación completa de todos los resultados aleatorios posibles.",
                    "El reintegro no se evalúa porque el modelo actual lo selecciona uniformemente y no aplica una hipótesis predictiva."
                ],
                Cases = cases
            };
        }

        private double[] CalculateWeightedProbabilities(List<Domain.Entities.WinningDraw> draws, DateTime asof, double halfLifeDays, double alpha)
        {
             // counts for numbers 1..49 (index 0..48)
             var counts = new double[49];
             
             foreach (var draw in draws)
             {
                 var ageDays = Math.Max(0, (asof - draw.DrawDate).TotalDays);
                 var weight = Math.Pow(0.5, ageDays / halfLifeDays);
                 
                 // Numbers are 1-based, array is 0-based
                 counts[draw.Number1 - 1] += weight;
                 counts[draw.Number2 - 1] += weight;
                 counts[draw.Number3 - 1] += weight;
                 counts[draw.Number4 - 1] += weight;
                 counts[draw.Number5 - 1] += weight;
                 counts[draw.Number6 - 1] += weight;
             }
             
             var totalCount = counts.Sum();
             var probabilities = new double[49];
             var denominator = totalCount + alpha * 49.0;
             
             for (int i = 0; i < 49; i++)
             {
                 probabilities[i] = (counts[i] + alpha) / denominator;
             }
             
             return probabilities;
        }

        private List<int> PickWeekly(double[] probabilities, Random random)
        {
            // Weighted random selection without replacement
            // "sample" mode from python script
            var availableNumbers = Enumerable.Range(1, 49).ToList();
            var currentProbs = probabilities.ToList(); 
            var result = new List<int>();

            for (int i = 0; i < 6; i++)
            {
                var picked = WeightedRandomPick(availableNumbers, currentProbs, random);
                result.Add(picked);
                
                // Remove picked number and its probability
                var index = availableNumbers.IndexOf(picked);
                availableNumbers.RemoveAt(index);
                currentProbs.RemoveAt(index);
                
                // Renormalize probabilities (optional but good for strict correctness, 
                // though basic subtraction from total sum is faster, here just using the list)
                // In Python `np.random.choice` with checks handles this. 
                // Here we re-normalize implicitly by summing currentProbs in next iteration.
            }

            return result.OrderBy(x => x).ToList();
        }

        private int WeightedRandomPick(List<int> numbers, List<double> probs, Random random)
        {
            var totalWeight = probs.Sum();
            var randomValue = random.NextDouble() * totalWeight;
            var cumulative = 0.0;
            
            for (int i = 0; i < numbers.Count; i++)
            {
                cumulative += probs[i];
                if (randomValue < cumulative)
                {
                    return numbers[i];
                }
            }
            return numbers.Last(); // Should not happen if logic is correct
        }

        private int PickReintegro(Random random)
        {
            return random.Next(0, 10);
        }

        private static List<int> GenerateUniformNumbers(Random random)
        {
            var numbers = Enumerable.Range(1, 49).ToArray();
            for (var index = numbers.Length - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                (numbers[index], numbers[swapIndex]) = (numbers[swapIndex], numbers[index]);
            }

            return numbers.Take(6).OrderBy(number => number).ToList();
        }

        private static List<int> GetNumbers(Domain.Entities.WinningDraw draw) =>
        [
            draw.Number1,
            draw.Number2,
            draw.Number3,
            draw.Number4,
            draw.Number5,
            draw.Number6
        ];

        private static int CountMatches(IEnumerable<int> predicted, IEnumerable<int> actual) =>
            predicted.Intersect(actual).Count();

        private static PredictionBacktestMetrics BuildMetrics(IEnumerable<int> matches)
        {
            var values = matches.ToList();
            var distribution = Enumerable.Range(0, 7)
                .ToDictionary(value => value, value => values.Count(matchesCount => matchesCount == value));

            return new PredictionBacktestMetrics
            {
                TotalMatches = values.Sum(),
                AverageMatches = values.Count == 0 ? 0 : values.Average(),
                MaximumMatches = values.Count == 0 ? 0 : values.Max(),
                DrawsWithAtLeastThreeMatches = values.Count(value => value >= 3),
                MatchDistribution = distribution
            };
        }

        private static void ValidateModelParameters(double halfLifeDays, double alpha)
        {
            if (halfLifeDays <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(halfLifeDays), "La vida media debe ser mayor que cero.");
            }

            if (alpha <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha debe ser mayor que cero.");
            }
        }

        private int GetVariationSeed(DateTime date, int variation) =>
            unchecked(GetIsoWeekSeed(date) + variation * 104729);

        private int GetUniformBaselineSeed(DateTime date) =>
            unchecked(GetIsoWeekSeed(date) ^ (date.DayOfYear * 397) ^ 0x5f3759df);

        private int GetIsoWeekSeed(DateTime date)
        {
            return ISOWeek.GetYear(date) * 100 + ISOWeek.GetWeekOfYear(date);
        }

    }
}
