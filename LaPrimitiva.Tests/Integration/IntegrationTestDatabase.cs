using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace LaPrimitiva.Tests.Integration;

internal static class IntegrationTestDatabase
{
    internal const string ConnectionStringEnvironmentVariable =
        "LAPRIMITIVA_INTEGRATION_TEST_CONNECTION";

    private const string SettingsFileName = "appsettings.IntegrationTests.json";
    private const string RequiredDatabaseSuffix = "_IntegrationTests";

    internal static string GetConnectionString()
    {
        var environmentValue = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);

        var connectionString = string.IsNullOrWhiteSpace(environmentValue)
            ? ReadConnectionStringFromSettings()
            : environmentValue;

        EnsureSafe(connectionString);
        return connectionString;
    }

    internal static void EnsureSafe(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se ha configurado una conexión para las pruebas de integración.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog?.Trim();

        if (!string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            throw new InvalidOperationException(
                "Las pruebas de integración no admiten AttachDBFilename.");
        }

        if (string.IsNullOrWhiteSpace(databaseName) ||
            !databaseName.EndsWith(RequiredDatabaseSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"La base de integración debe terminar en '{RequiredDatabaseSuffix}'. " +
                $"Se recibió '{databaseName ?? "<sin nombre>"}'.");
        }
    }

    internal static string CreateIsolatedConnectionString(string connectionString)
    {
        EnsureSafe(connectionString);

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databasePrefix = builder.InitialCatalog[..^RequiredDatabaseSuffix.Length];
        var runId = $"{Environment.ProcessId}_{Guid.NewGuid():N}";

        builder.InitialCatalog = $"{databasePrefix}_{runId}{RequiredDatabaseSuffix}";
        EnsureSafe(builder.ConnectionString);

        return builder.ConnectionString;
    }

    private static string ReadConnectionStringFromSettings()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);

        if (!File.Exists(settingsPath))
        {
            throw new InvalidOperationException(
                $"No se encontró la configuración de pruebas '{settingsPath}'.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var connectionString = document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        return connectionString
            ?? throw new InvalidOperationException(
                $"'{SettingsFileName}' no contiene ConnectionStrings:DefaultConnection.");
    }
}
