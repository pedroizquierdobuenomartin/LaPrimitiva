using LaPrimitiva.Tests.Integration;

namespace LaPrimitiva.Tests;

public class TestDatabaseSafetyTests
{
    [Theory]
    [InlineData("Server=localhost\\SQLEXPRESS;Database=PrimitivaAuditV2;Trusted_Connection=True")]
    [InlineData("Server=localhost\\SQLEXPRESS;Database=OtraBase;Trusted_Connection=True")]
    [InlineData("Server=localhost\\SQLEXPRESS;Trusted_Connection=True")]
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
}
