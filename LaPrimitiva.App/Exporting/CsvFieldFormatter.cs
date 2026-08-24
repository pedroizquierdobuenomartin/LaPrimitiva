namespace LaPrimitiva.App.Exporting;

public static class CsvFieldFormatter
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    public static string Encode(string? value)
    {
        value ??= string.Empty;

        if (value.Length > 0 && FormulaPrefixes.Contains(value[0]))
        {
            value = $"'{value}";
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
