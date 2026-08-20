using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Infrastructure.Persistence;
using LaPrimitiva.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaPrimitiva.Tests.Integration
{
    [Collection(IntegrationTestCollection.Name)]
    public class WinningDrawSeederTests : IntegrationTestBase
    {
        private readonly IntegrationTestFixture _fixture;

        public WinningDrawSeederTests(IntegrationTestFixture fixture) : base(fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SeedHistoricalData_ShouldInsertRecords()
        {
            // Arrange
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
            var seeder = new WinningDrawSeeder(context);
            var csvPath = Path.Combine(_fixture.TestDataDirectory, "winning-draws.csv");

            // Act
            await seeder.SeedAsync(csvPath);

            // Assert
            var count = await context.WinningDraws.CountAsync();
            Assert.True(count > 0);
            Console.WriteLine($"Total draws in DB: {count}");
        }
    }
}
