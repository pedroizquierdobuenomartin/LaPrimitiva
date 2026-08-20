using LaPrimitiva.Tests.Integration;
using Microsoft.Data.SqlClient;

namespace LaPrimitiva.Tests;

public class TestDatabaseSafetyTests
{
    [Theory]
    [InlineData("Server=localhost\\SQLEXPRESS;Database=PrimitivaAuditV2;Trusted_Connection=True")]
    [InlineData("Server=localhost\\SQLEXPRESS;Database=OtraBase;Trusted_Connection=True")]
    [InlineData("Server=localhost\\SQLEXPRESS;Trusted_Connection=True")]
    [InlineData("Server=(LocalDB)\\MSSQLLocalDB;Database=PrimitivaAuditV2_IntegrationTests;AttachDBFilename=development.mdf;Integrated Security=True")]
    public void EnsureSafe_ShouldRejectNonTestDatabases(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(
            () => IntegrationTestDatabase.EnsureSafe(connectionString));
    }

    [Fact]
    public void EnsureSafe_ShouldAcceptDedicatedIntegrationDatabase()
    {
        var exception = Record.Exception(() => IntegrationTestDatabase.EnsureSafe(
            "Server=localhost\\SQLEXPRESS;Database=PrimitivaAuditV2_IntegrationTests;Trusted_Connection=True"));

        Assert.Null(exception);
    }

    [Fact]
    public void CreateIsolatedConnectionString_ShouldCreateUniqueSafeDatabaseNames()
    {
        const string configuredConnection =
            "Server=localhost\\SQLEXPRESS;Database=PrimitivaAuditV2_IntegrationTests;Trusted_Connection=True";

        var first = new SqlConnectionStringBuilder(
            IntegrationTestDatabase.CreateIsolatedConnectionString(configuredConnection));
        var second = new SqlConnectionStringBuilder(
            IntegrationTestDatabase.CreateIsolatedConnectionString(configuredConnection));

        Assert.EndsWith("_IntegrationTests", first.InitialCatalog, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("_IntegrationTests", second.InitialCatalog, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(first.InitialCatalog, second.InitialCatalog);
    }
}
