using LaPrimitiva.App.Exporting;

namespace LaPrimitiva.Tests;

public class CsvFieldFormatterTests
{
    [Theory]
    [InlineData("=1+1", "\"'=1+1\"")]
    [InlineData("+SUM(A1:A2)", "\"'+SUM(A1:A2)\"")]
    [InlineData("-2+3", "\"'-2+3\"")]
    [InlineData("@SUM(A1:A2)", "\"'@SUM(A1:A2)\"")]
    public void Encode_WithFormulaPrefix_NeutralizesFormula(string value, string expected)
    {
        var result = CsvFieldFormatter.Encode(value);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Encode_WithQuotesCommasAndLineBreaks_PreservesCsvContent()
    {
        const string value = "primera, \"cita\"\r\nsegunda\ntercera";

        var result = CsvFieldFormatter.Encode(value);

        Assert.Equal("\"primera, \"\"cita\"\"\r\nsegunda\ntercera\"", result);
    }
}
