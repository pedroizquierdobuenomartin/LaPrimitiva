using System.Globalization;
using LaPrimitiva.App.Exporting;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Tests;

public class CsvExportBuilderTests
{
    [Fact]
    public void Build_WithSpanishCurrentCulture_UsesInvariantDecimalsAndValidCsvEscaping()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");

        try
        {
            var draw = new DrawRecord
            {
                WeekNumber = 1,
                DrawType = DrawType.Lunes,
                DrawDate = new DateTime(2026, 1, 5),
                Played = true,
                CosteFija = 1.1m,
                CosteAuto = 2.2m,
                CosteJokerFija = 3.3m,
                CosteJokerAuto = 4.4m,
                TotalCoste = 11m,
                FixedPrize = 5.5m,
                AutoPrize = 6.6m,
                JokerFixedPrize = 7.7m,
                JokerAutoPrize = 8.8m,
                TotalPremios = 28.6m,
                Neto = 17.6m,
                Acumulado = -3.3m,
                Notes = "=SUM(A1,A2)\r\n\"nota\""
            };

            var result = CsvExportBuilder.Build([draw]);
            var firstLineBreak = result.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            var dataRow = result[(firstLineBreak + Environment.NewLine.Length)..];

            Assert.Equal(
                "1,Lunes,2026-01-05,1,1.1,2.2,3.3,4.4,11,5.5,6.6,7.7,8.8,28.6,17.6,-3.3,\"'=SUM(A1,A2)\r\n\"\"nota\"\"\"" + Environment.NewLine,
                dataRow);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
