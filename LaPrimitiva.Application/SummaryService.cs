using System;
using System.Collections.Generic;
using System.Linq;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Application.Services
{
    public class SummaryDto
    {
        public int TotalDraws { get; set; }
        public int PlayedDraws { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalWon { get; set; }
        public decimal NetResult => TotalWon - TotalSpent;
        public decimal ROI => TotalSpent > 0 ? (TotalWon / TotalSpent) * 100 : 0;
        public int WinningDraws { get; set; }
        public double WinningPercentage => TotalDraws > 0 ? (double)WinningDraws / TotalDraws * 100 : 0;

        // Breakdown
        public decimal FixedSpent { get; set; }
        public decimal FixedWon { get; set; }
        public decimal AutoSpent { get; set; }
        public decimal AutoWon { get; set; }
        public decimal JokerFixedSpent { get; set; }
        public decimal JokerFixedWon { get; set; }
        public decimal JokerAutoSpent { get; set; }
        public decimal JokerAutoWon { get; set; }
    }

    public class MonthlySummaryDto
    {
        public string MonthName { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Spent { get; set; }
        public decimal Won { get; set; }
        public decimal Net => Won - Spent;
    }

    public class SummaryService
    {
        public SummaryDto GetSummary(IEnumerable<DrawRecord> draws)
        {
            var summary = new SummaryDto();
            var drawList = draws.ToList();

            summary.TotalDraws = drawList.Count;
            summary.PlayedDraws = drawList.Count(d => d.Played);
            summary.WinningDraws = drawList.Count(d => d.Played && d.TotalPremios > 0);

            foreach (var d in drawList)
            {
                summary.TotalSpent += d.TotalCoste;
                summary.TotalWon += d.TotalPremios;

                summary.FixedSpent += d.CosteFija;
                summary.FixedWon += d.FixedPrize;
                summary.AutoSpent += d.CosteAuto;
                summary.AutoWon += d.AutoPrize;
                summary.JokerFixedSpent += d.CosteJokerFija;
                summary.JokerFixedWon += d.JokerFixedPrize;
                summary.JokerAutoSpent += d.CosteJokerAuto;
                summary.JokerAutoWon += d.JokerAutoPrize;
            }

            return summary;
        }

        public List<MonthlySummaryDto> GetMonthlySummaries(IEnumerable<DrawRecord> draws)
        {
            return draws
                .GroupBy(d => new { d.DrawDate.Year, d.DrawDate.Month })
                .Select(g => new MonthlySummaryDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM"),
                    Spent = g.Sum(d => d.TotalCoste),
                    Won = g.Sum(d => d.TotalPremios)
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();
        }
    }
}
