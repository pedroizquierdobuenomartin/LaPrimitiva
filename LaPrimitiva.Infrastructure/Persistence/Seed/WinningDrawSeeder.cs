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
        private readonly IDbContextFactory<PrimitivaDbContext> _contextFactory;

        public WinningDrawSeeder(IDbContextFactory<PrimitivaDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private int ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            return int.TryParse(value, out var result) ? result : 0;
        }

        private async Task RepairFinancialTotalsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inconsistentDraws = await context.DrawRecords
                .Where(draw =>
                    draw.TotalCoste != draw.CosteFija + draw.CosteAuto + draw.CosteJokerFija + draw.CosteJokerAuto ||
                    draw.TotalPremios != draw.FixedPrize + draw.AutoPrize + draw.JokerFixedPrize + draw.JokerAutoPrize ||
                    draw.Neto != draw.TotalPremios - draw.TotalCoste ||
                    (!draw.Played &&
                        (draw.CosteFija != 0 || draw.CosteAuto != 0 ||
                         draw.CosteJokerFija != 0 || draw.CosteJokerAuto != 0 ||
                         draw.FixedPrize != 0 || draw.AutoPrize != 0 ||
                         draw.JokerFixedPrize != 0 || draw.JokerAutoPrize != 0)))
                .ToListAsync();

            foreach (var draw in inconsistentDraws)
            {
                draw.RecalculateFinancials();
            }

            if (inconsistentDraws.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }

        public async Task SeedFromDirectoryAsync(string directoryPath)
        {
            await RepairFinancialTotalsAsync();

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
            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"CSV file not found: {csvPath}");
                return;
            }

            var lines = await File.ReadAllLinesAsync(csvPath);
            if (lines.Length <= 1) return;

            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingDates = await context.WinningDraws
                .AsNoTracking()
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

                    draw.Validate();
                    newDraws.Add(draw);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line: {line}. {ex.Message}");
                }
            }

            if (newDraws.Any())
            {
                await context.WinningDraws.AddRangeAsync(newDraws);
                await context.SaveChangesAsync();
                Console.WriteLine($"Seeded {newDraws.Count} draws from {Path.GetFileName(csvPath)}");
            }
        }
    }
}
