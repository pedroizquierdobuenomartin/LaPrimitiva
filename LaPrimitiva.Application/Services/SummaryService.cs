using System;
using System.Collections.Generic;
using System.Linq;
using LaPrimitiva.Application.DTOs;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Application.Services
{
    /// <summary>
    /// Servicio para calcular resúmenes y estadísticas de los sorteos.
    /// </summary>
    public class SummaryService
    {
        /// <summary>
        /// Calcula el resumen global de una lista de sorteos.
        /// </summary>
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
