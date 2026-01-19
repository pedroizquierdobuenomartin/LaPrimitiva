
namespace LaPrimitiva.Application.DTOs
{
    public class MonthlySummaryDto
    {
        public string MonthName { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Spent { get; set; }
        public decimal Won { get; set; }
        public decimal Net => Won - Spent;
    }
}
