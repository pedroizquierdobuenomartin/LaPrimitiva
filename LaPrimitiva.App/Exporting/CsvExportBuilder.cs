using System.Globalization;
using System.Text;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.App.Exporting;

public static class CsvExportBuilder
{
    private const string Header = "Semana #,Sorteo,Fecha,Jugado,Coste Fija (€),Coste Auto (€),Coste Joker Fija (€),Coste Joker Auto (€),Total Coste (€),Premio Fija (€),Premio Auto (€),Premio Joker Fija (€),Premio Joker Auto (€),Total Premios (€),Neto (€),Neto Acumulado (€),Notas";

    public static string Build(IEnumerable<DrawRecord> draws)
    {
        var csv = new StringBuilder();
        csv.AppendLine(Header);

        foreach (var draw in draws)
        {
            csv.AppendLine(string.Join(",",
                draw.WeekNumber.ToString(CultureInfo.InvariantCulture),
                draw.DrawType.ToString(),
                draw.DrawDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                draw.Played ? "1" : "0",
                FormatDecimal(draw.CosteFija),
                FormatDecimal(draw.CosteAuto),
                FormatDecimal(draw.CosteJokerFija),
                FormatDecimal(draw.CosteJokerAuto),
                FormatDecimal(draw.TotalCoste),
                FormatDecimal(draw.FixedPrize),
                FormatDecimal(draw.AutoPrize),
                FormatDecimal(draw.JokerFixedPrize),
                FormatDecimal(draw.JokerAutoPrize),
                FormatDecimal(draw.TotalPremios),
                FormatDecimal(draw.Neto),
                FormatDecimal(draw.Acumulado),
                CsvFieldFormatter.Encode(draw.Notes)));
        }

        return csv.ToString();
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
