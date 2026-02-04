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
    public class WinningDrawSeederTests
    {
        private PrimitivaDbContext GetDbContext()
        {
            var services = new ServiceCollection();
            services.AddDbContext<PrimitivaDbContext>(options =>
                options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PrimitivaAuditV2;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"));
            
            var serviceProvider = services.BuildServiceProvider();
            return serviceProvider.GetRequiredService<PrimitivaDbContext>();
        }

        [Fact]
        public async Task SeedHistoricalData_ShouldInsertRecords()
        {
            // Arrange
            var context = GetDbContext();
            var seeder = new WinningDrawSeeder(context);
            
            var csv1 = @"f:\Repositorios\LaPrimitiva\.agent\assests\Histórico de Resultados - Primitiva - 1985 a 2012.csv";
            var csv2 = @"f:\Repositorios\LaPrimitiva\.agent\assests\Histórico de Resultados - Primitiva - 2013 a 2025.csv";

            // Act
            await seeder.SeedAsync(csv1);
            await seeder.SeedAsync(csv2);

            // Assert
            var count = await context.WinningDraws.CountAsync();
            Assert.True(count > 0);
            Console.WriteLine($"Total draws in DB: {count}");
        }
    }
}
