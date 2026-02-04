using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LaPrimitiva.Infrastructure.Persistence.Seed
{
    public class WinningDrawSeeder
    {
        private readonly PrimitivaDbContext _context;

        public WinningDrawSeeder(PrimitivaDbContext context)
        {
            _context = context;
        }

        private int ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            return int.TryParse(value, out var result) ? result : 0;
        }

        private async Task EnsureAllTablesExistAsync()
        {
            // Note: We use raw SQL with IF NOT EXISTS logic to be safe across environments 
            // and avoid "already exists" errors during CI/CD or first run.

            var sqlPlans = @"
                IF OBJECT_ID(N'[Plans]') IS NULL
                BEGIN
                    CREATE TABLE [Plans] (
                        [Id] uniqueidentifier NOT NULL,
                        [Name] nvarchar(max) NOT NULL,
                        [EffectiveFrom] datetime2 NOT NULL,
                        [EffectiveTo] datetime2 NULL,
                        [WeeksToTrackDefault] int NOT NULL,
                        [CostPerBet] decimal(10,2) NOT NULL,
                        [BetsPerDraw] int NOT NULL,
                        [EnableJoker] bit NOT NULL,
                        [JokerCostPerBet] decimal(10,2) NOT NULL,
                        [FixedCombinationLabel] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_Plans] PRIMARY KEY ([Id])
                    );
                END;";

            var sqlDrawRecords = @"
                IF OBJECT_ID(N'[DrawRecords]') IS NULL
                BEGIN
                    CREATE TABLE [DrawRecords] (
                        [Id] uniqueidentifier NOT NULL,
                        [PlanId] uniqueidentifier NOT NULL,
                        [DrawType] tinyint NOT NULL,
                        [DrawDate] datetime2 NOT NULL,
                        [WeekNumber] int NOT NULL,
                        [Played] bit NOT NULL,
                        [FixedPrize] decimal(10,2) NOT NULL,
                        [AutoPrize] decimal(10,2) NOT NULL,
                        [JokerFixedPrize] decimal(10,2) NOT NULL,
                        [JokerAutoPrize] decimal(10,2) NOT NULL,
                        [Notes] nvarchar(max) NULL,
                        [CosteFija] decimal(10,2) NOT NULL DEFAULT 0,
                        [CosteAuto] decimal(10,2) NOT NULL DEFAULT 0,
                        [CosteJokerFija] decimal(10,2) NOT NULL DEFAULT 0,
                        [CosteJokerAuto] decimal(10,2) NOT NULL DEFAULT 0,
                        [TotalCoste] decimal(10,2) NOT NULL DEFAULT 0,
                        [TotalPremios] decimal(10,2) NOT NULL DEFAULT 0,
                        [Neto] decimal(10,2) NOT NULL DEFAULT 0,
                        [Acumulado] decimal(12,2) NOT NULL DEFAULT 0,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_DrawRecords] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_DrawRecords_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_DrawRecords_DrawDate] ON [DrawRecords] ([DrawDate]);
                    CREATE UNIQUE INDEX [IX_DrawRecords_PlanId_DrawDate_DrawType] ON [DrawRecords] ([PlanId], [DrawDate], [DrawType]);
                    CREATE INDEX [IX_DrawRecords_WeekNumber] ON [DrawRecords] ([WeekNumber]);
                END;";

            var sqlWinningDraws = @"
                IF OBJECT_ID(N'[WinningDraws]') IS NULL
                BEGIN
                    CREATE TABLE [WinningDraws] (
                        [Id] uniqueidentifier NOT NULL,
                        [DrawDate] datetime2 NOT NULL,
                        [Number1] int NOT NULL,
                        [Number2] int NOT NULL,
                        [Number3] int NOT NULL,
                        [Number4] int NOT NULL,
                        [Number5] int NOT NULL,
                        [Number6] int NOT NULL,
                        [Complementario] int NOT NULL,
                        [Reintegro] int NOT NULL,
                        [Joker] nvarchar(10) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_WinningDraws] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_WinningDraws_DrawDate] ON [WinningDraws] ([DrawDate]);
                END;";

            await _context.Database.ExecuteSqlRawAsync(sqlPlans);
            await _context.Database.ExecuteSqlRawAsync(sqlDrawRecords);
            await _context.Database.ExecuteSqlRawAsync(sqlWinningDraws);
        }

        public async Task SeedFromDirectoryAsync(string directoryPath)
        {
            await EnsureAllTablesExistAsync();

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Seed directory not found: {directoryPath}");
                return;
            }

            var csvFiles = Directory.GetFiles(directoryPath, "*.csv");
            foreach (var file in csvFiles)
            {
                await SeedAsync(file);
            }
        }

        public async Task SeedAsync(string csvPath)
        {
            if (_context.WinningDraws.Any()) 
            {
                // Optimization: if we already have data, we might want to skip or just check for new ones.
                // But since it's a seed of historical data, if there's anything, let's skip for speed 
                // UNLESS user wants to merge. For now, let's keep it idempotent.
            }
            
            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"CSV file not found: {csvPath}");
                return;
            }

            var lines = await File.ReadAllLinesAsync(csvPath);
            if (lines.Length <= 1) return;

            var existingDates = await _context.WinningDraws
                .Select(wd => wd.DrawDate)
                .ToListAsync();

            var newDraws = new List<WinningDraw>();

            // Skip header
            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 9) continue;

                try
                {
                    if (!DateTime.TryParseExact(parts[0], "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        if (!DateTime.TryParseExact(parts[0], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                        {
                            Console.WriteLine($"Could not parse date: {parts[0]}");
                            continue;
                        }
                    }

                    if (existingDates.Contains(date) || newDraws.Any(d => d.DrawDate == date))
                    {
                        continue;
                    }

                    var draw = new WinningDraw
                    {
                        DrawDate = date,
                        Number1 = ParseInt(parts[1]),
                        Number2 = ParseInt(parts[2]),
                        Number3 = ParseInt(parts[3]),
                        Number4 = ParseInt(parts[4]),
                        Number5 = ParseInt(parts[5]),
                        Number6 = ParseInt(parts[6]),
                        Complementario = ParseInt(parts[7]),
                        Reintegro = ParseInt(parts[8]),
                        Joker = (parts.Length > 9 && !string.IsNullOrWhiteSpace(parts[9])) ? parts[9] : null
                    };

                    newDraws.Add(draw);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line: {line}. {ex.Message}");
                }
            }

            if (newDraws.Any())
            {
                await _context.WinningDraws.AddRangeAsync(newDraws);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Seeded {newDraws.Count} draws from {Path.GetFileName(csvPath)}");
            }
        }
    }
}
