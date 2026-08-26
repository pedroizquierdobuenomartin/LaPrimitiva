
using LaPrimitiva.Domain.Services;

namespace LaPrimitiva.Application.DTOs
{
    public class SummaryDto
    {
        public int TotalDraws { get; set; }
        public int PlayedDraws { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalWon { get; set; }
        public decimal NetResult => FinancialMetrics.CalculateNet(TotalSpent, TotalWon);
        public decimal ROI => FinancialMetrics.CalculateRoi(TotalSpent, TotalWon);
        public int WinningDraws { get; set; }
        public double WinningPercentage => FinancialMetrics.CalculatePercentage(WinningDraws, PlayedDraws);

        // Breakdown
        public decimal FixedSpent { get; set; }
        public decimal FixedWon { get; set; }
        public decimal AutoSpent { get; set; }
        public decimal AutoWon { get; set; }
        public decimal JokerFixedSpent { get; set; }
        public decimal JokerFixedWon { get; set; }
        public decimal JokerAutoSpent { get; set; }
        public decimal JokerAutoWon { get; set; }

        public decimal FixedNet => FinancialMetrics.CalculateNet(FixedSpent, FixedWon);
        public decimal AutoNet => FinancialMetrics.CalculateNet(AutoSpent, AutoWon);
        public decimal JokerSpent => JokerFixedSpent + JokerAutoSpent;
        public decimal JokerWon => JokerFixedWon + JokerAutoWon;
        public decimal JokerNet => FinancialMetrics.CalculateNet(JokerSpent, JokerWon);
    }
}
