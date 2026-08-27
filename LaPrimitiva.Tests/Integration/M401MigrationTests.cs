using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LaPrimitiva.Tests.Integration;

public sealed class M401MigrationTests
{
    private const string PreviousReleaseMigration = "20260824150000_ValidateWinningDraws";

    [Fact]
    public async Task Migrations_CreateTheCompleteSchema_FromScratch()
    {
        var connectionString = CreateConnectionString();

        try
        {
            await using var context = CreateContext(connectionString);

            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

            var applied = (await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).ToArray();
            var defined = context.Database.GetMigrations().ToArray();
            Assert.Equal(defined, applied);
            Assert.True(await context.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE [name] IN ('Plans', 'DrawRecords', 'WinningDraws')")
                .SingleAsync(TestContext.Current.CancellationToken) == 3);
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
                await legacyContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
                legacyContext.AddRange(plan, winningDraw);
                await legacyContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var migratedContext = CreateContext(connectionString))
            {
                await migratedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

                var defined = migratedContext.Database.GetMigrations().ToArray();
                var applied = (await migratedContext.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).ToArray();
                Assert.Equal(defined, applied);
                Assert.Equal("Plan conservado", (await migratedContext.Plans.SingleAsync(TestContext.Current.CancellationToken)).Name);
                Assert.Equal("0123456", (await migratedContext.WinningDraws.SingleAsync(TestContext.Current.CancellationToken)).Joker);
            }
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task Migrations_UpgradeFromPreviousVersion_WithoutLosingData()
    {
        var connectionString = CreateConnectionString();
        var planId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

        try
        {
            await using (var previousVersionContext = CreateContext(connectionString))
            {
                var migrator = previousVersionContext.GetService<IMigrator>();
                await migrator.MigrateAsync(PreviousReleaseMigration, TestContext.Current.CancellationToken);

                await previousVersionContext.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO [Plans] (
                        [Id], [Name], [EffectiveFrom], [EffectiveTo],
                        [WeeksToTrackDefault], [CostPerBet], [BetsPerDraw],
                        [EnableJoker], [JokerCostPerBet], [FixedCombinationLabel],
                        [CreatedAt], [UpdatedAt])
                    VALUES (
                        {planId}, {"Plan de versión anterior"}, {new DateTime(2026, 1, 1)}, NULL,
                        {52}, {1m}, {1},
                        {false}, {0m}, NULL,
                        {createdAt}, {createdAt});",
                    TestContext.Current.CancellationToken);
            }

            await using (var currentVersionContext = CreateContext(connectionString))
            {
                await currentVersionContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

                var plan = await currentVersionContext.Plans.SingleAsync(
                    candidate => candidate.Id == planId,
                    TestContext.Current.CancellationToken);
                Assert.Equal("Plan de versión anterior", plan.Name);
                Assert.Equal(createdAt, plan.CreatedAt);
                Assert.NotEmpty(plan.RowVersion);

                var defined = currentVersionContext.Database.GetMigrations().ToArray();
                var applied = (await currentVersionContext.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).ToArray();
                Assert.Equal(defined, applied);
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
