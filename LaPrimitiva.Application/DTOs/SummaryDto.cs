
namespace LaPrimitiva.Application.DTOs
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
}
