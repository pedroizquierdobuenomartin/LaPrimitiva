using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Domain.Services;

/// <summary>
/// Reglas financieras puras compartidas por entidades y casos de uso.
/// </summary>
public static class FinancialMetrics
{
    public static decimal CalculateNet(decimal spent, decimal won) => won - spent;

    public static decimal CalculateRoi(decimal spent, decimal won) =>
        spent > 0 ? CalculateNet(spent, won) / spent * 100 : 0;

    public static double CalculatePercentage(int part, int total) =>
        total > 0 ? (double)part / total * 100 : 0;

    public static int CountWinningBets(DrawRecord draw) =>
        (draw.FixedPrize > 0 ? 1 : 0) +
        (draw.AutoPrize > 0 ? 1 : 0) +
        (draw.JokerFixedPrize > 0 ? 1 : 0) +
        (draw.JokerAutoPrize > 0 ? 1 : 0);
}
