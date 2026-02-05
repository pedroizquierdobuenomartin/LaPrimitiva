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

        public async Task<CombinationResult> GenerateCombinationAsync(double halfLifeDays = 365.0, double alpha = 1.0)
        {
            // 1. Fetch History
            var draws = await _repository.GetListAsync();
            var sortedDraws = draws.OrderBy(d => d.DrawDate).ToList();

            if (!sortedDraws.Any())
            {
                // Fallback if no history
                 return GenerateRandomCombination(null);
            }

            var asof = DateTime.Today;

            // 2. Calculate Weighted Probabilities
            var probabilities = CalculateWeightedProbabilities(sortedDraws, asof, halfLifeDays, alpha);

            // 3. Pick Weighted Random Numbers
            var seed = GetIsoWeekSeed(asof);
            var random = new Random(seed);
            
            var pickedNumbers = PickWeekly(probabilities, random);
            var reintegro = PickReintegro(random);

             // 4. Debug Info (Chi-Square) - simplified
             var (chi2, pValue) = CalculateChiSquareUniformity(sortedDraws);

            return new CombinationResult
            {
                Numbers = pickedNumbers,
                Reintegro = reintegro,
                DebugInfo = new Dictionary<string, object>
                {
                    { "half_life_days", halfLifeDays },
                    { "alpha", alpha },
                    { "draws_analyzed", sortedDraws.Count },
                    { "chi2_uniformity", chi2 },
                    { "pvalue_uniformity", pValue }, // Note: P-Value calculation requires a statistical library, simplified here or placeholder
                    { "top10_by_model", probabilities.Select((p, i) => new { Num = i + 1, Prob = p }).OrderByDescending(x => x.Prob).Take(10).Select(x => x.Num).ToList() }
                }
            };
        }
        
        private CombinationResult GenerateRandomCombination(int? seed)
        {
             var random = seed.HasValue ? new Random(seed.Value) : new Random();
             var numbers = Enumerable.Range(1, 49).OrderBy(x => random.Next()).Take(6).OrderBy(x => x).ToList();
             var reintegro = random.Next(0, 10);
             
             return new CombinationResult { Numbers = numbers, Reintegro = reintegro, DebugInfo = new Dictionary<string, object> { { "note", "Random fallback (no history)" } } };
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

        private int GetIsoWeekSeed(DateTime date)
        {
             // ISO 8601 Week
             Calendar calendar = CultureInfo.InvariantCulture.Calendar;
             int day = (int)calendar.GetDayOfWeek(date);
             if (day >= 1 && day <= 3) // Monday-Wednesday
             {
                date = date.AddDays(3);
             }
             
             int w = calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
             int y = date.Year;
             
             // Adjust year if week is 52/53 of previous year or 1 of next year
             // But simpler heuristic from python: `y, w, _ = d.isocalendar()` which handles this naturally
             
             // .NET's ISOWeek class is available in newer .NET versions, checking...
             // Assuming .NET 6+, we can use ISOWeek. 
             // If not, use the logic above but carefully. 
             // Let's stick to a robust implementation or ISOWeek.
             
             return ISOWeek.GetYear(date) * 100 + ISOWeek.GetWeekOfYear(date);
        }

        private (double chi2, double pValue) CalculateChiSquareUniformity(List<Domain.Entities.WinningDraw> draws)
        {
             var counts = new double[49];
             foreach (var draw in draws)
             {
                 counts[draw.Number1 - 1]++;
                 counts[draw.Number2 - 1]++;
                 counts[draw.Number3 - 1]++;
                 counts[draw.Number4 - 1]++;
                 counts[draw.Number5 - 1]++;
                 counts[draw.Number6 - 1]++;
             }
             
             var totalNumbers = draws.Count * 6;
             var expected = totalNumbers / 49.0;
             var chi2 = 0.0;
             
             foreach (var c in counts)
             {
                 chi2 += Math.Pow(c - expected, 2) / expected;
             }
             
             // Approximate P-Value (requires Chi2 dist function)
             // Python uses scipy.stats.chi2.sf(chi2, df=48)
             // We will return -1 for P-Value as we don't have a math lib, allowing UI to handle it.
             return (chi2, -1.0);
        }
    }
}
