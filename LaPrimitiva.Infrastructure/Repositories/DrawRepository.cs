using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LaPrimitiva.Infrastructure.Repositories
{
    /// <summary>
    /// Implementación de IDrawRepository utilizando EF Core 10.
    /// </summary>
    public class DrawRepository(IDbContextFactory<PrimitivaDbContext> contextFactory) : IDrawRepository
    {
        private readonly IDbContextFactory<PrimitivaDbContext> _contextFactory = contextFactory;

        public async Task<List<DrawRecord>> GetListAsync(Expression<Func<DrawRecord, bool>>? predicate = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.DrawRecords.AsNoTracking();
            if (predicate != null) query = query.Where(predicate);
            return await query.ToListAsync();
        }

        public async Task<bool> AnyAsync(Expression<Func<DrawRecord, bool>> predicate)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.DrawRecords.AnyAsync(predicate);
        }

        public async Task CreateRangeAsync(IEnumerable<DrawRecord> draws)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var drawList = draws.ToList();
            foreach (var draw in drawList)
            {
                draw.RecalculateFinancials();
            }

            await context.DrawRecords.AddRangeAsync(drawList);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(DrawRecord draw)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var tracked = await GetTrackedDrawAsync(context, draw.Id);
            ApplyEditableValues(tracked, draw);
            await context.SaveChangesAsync();
        }

        public async Task UpdateRangeAsync(IEnumerable<DrawRecord> draws)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var disconnectedDraws = draws.ToList();
            var drawIds = disconnectedDraws.Select(draw => draw.Id).ToList();
            var trackedDraws = await context.DrawRecords
                .Where(draw => drawIds.Contains(draw.Id))
                .ToDictionaryAsync(draw => draw.Id);

            var missingId = drawIds
                .Where(id => !trackedDraws.ContainsKey(id))
                .Cast<Guid?>()
                .FirstOrDefault();
            if (missingId.HasValue)
            {
                throw new InvalidOperationException($"No existe el sorteo con identificador '{missingId}'.");
            }

            foreach (var draw in disconnectedDraws)
            {
                ApplyEditableValues(trackedDraws[draw.Id], draw);
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.DrawRecords
                .Where(d => d.Id == id)
                .ExecuteDeleteAsync();
        }

        public async Task DeleteRangeAsync(Expression<Func<DrawRecord, bool>> predicate)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.DrawRecords
                .Where(predicate)
                .ExecuteDeleteAsync();
        }

        private static async Task<DrawRecord> GetTrackedDrawAsync(PrimitivaDbContext context, Guid id)
        {
            return await context.DrawRecords.SingleOrDefaultAsync(draw => draw.Id == id)
                ?? throw new InvalidOperationException($"No existe el sorteo con identificador '{id}'.");
        }

        private static void ApplyEditableValues(DrawRecord target, DrawRecord source)
        {
            source.RecalculateFinancials();
            target.Played = source.Played;
            target.FixedPrize = source.FixedPrize;
            target.AutoPrize = source.AutoPrize;
            target.JokerFixedPrize = source.JokerFixedPrize;
            target.JokerAutoPrize = source.JokerAutoPrize;
            target.Notes = source.Notes;
            target.CosteFija = source.CosteFija;
            target.CosteAuto = source.CosteAuto;
            target.CosteJokerFija = source.CosteJokerFija;
            target.CosteJokerAuto = source.CosteJokerAuto;
            target.TotalCoste = source.TotalCoste;
            target.TotalPremios = source.TotalPremios;
            target.Neto = source.Neto;
            target.UpdatedAt = source.UpdatedAt;
        }
    }
}
