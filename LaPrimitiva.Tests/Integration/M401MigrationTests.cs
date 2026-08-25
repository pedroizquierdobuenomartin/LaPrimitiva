using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LaPrimitiva.Tests.Integration;

public sealed class M401MigrationTests
{
    [Fact]
    public async Task Migrations_CreateTheCompleteSchema_FromScratch()
    {
        var connectionString = CreateConnectionString();

        try
        {
            await using var context = CreateContext(connectionString);

            await context.Database.MigrateAsync();

            var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
            var defined = context.Database.GetMigrations().ToArray();
            Assert.Equal(defined, applied);
            Assert.True(await context.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE [name] IN ('Plans', 'DrawRecords', 'WinningDraws')").SingleAsync() == 3);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task Migrations_AdoptLegacySchema_WithoutLosingData()
    {
        var connectionString = CreateConnectionString();
        var plan = new Plan
        {
            Name = "Plan conservado",
            EffectiveFrom = new DateTime(2026, 1, 1),
            WeeksToTrackDefault = 4,
            CostPerBet = 1m,
            BetsPerDraw = 1,
            EnableJoker = false,
            JokerCostPerBet = 0m
        };
        var winningDraw = new WinningDraw
        {
            DrawDate = new DateTime(2026, 1, 3),
            Number1 = 1,
            Number2 = 2,
            Number3 = 3,
            Number4 = 4,
            Number5 = 5,
            Number6 = 6,
            Complementario = 7,
            Reintegro = 8,
            Joker = "0123456"
        };

        try
        {
            await using (var legacyContext = CreateContext(connectionString))
            {
                // EnsureCreated reproduces the former startup-created schema: current
                // tables exist, contain data and have no __EFMigrationsHistory rows.
                await legacyContext.Database.EnsureCreatedAsync();
                legacyContext.AddRange(plan, winningDraw);
                await legacyContext.SaveChangesAsync();
            }

            await using (var migratedContext = CreateContext(connectionString))
            {
                await migratedContext.Database.MigrateAsync();

                var defined = migratedContext.Database.GetMigrations().ToArray();
                var applied = (await migratedContext.Database.GetAppliedMigrationsAsync()).ToArray();
                Assert.Equal(defined, applied);
                Assert.Equal("Plan conservado", (await migratedContext.Plans.SingleAsync()).Name);
                Assert.Equal("0123456", (await migratedContext.WinningDraws.SingleAsync()).Joker);
            }
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static string CreateConnectionString() =>
        IntegrationTestDatabase.CreateIsolatedConnectionString(
            IntegrationTestDatabase.GetConnectionString());

    private static PrimitivaDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new PrimitivaDbContext(options);
    }

    private static async Task DeleteDatabaseAsync(string connectionString)
    {
        IntegrationTestDatabase.EnsureSafe(connectionString);
        await using var context = CreateContext(connectionString);
        await context.Database.EnsureDeletedAsync();
    }
}
