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
    public class DrawRepository(PrimitivaDbContext context) : IDrawRepository
    {
        private readonly PrimitivaDbContext _context = context;

        public async Task<List<DrawRecord>> GetListAsync(Expression<Func<DrawRecord, bool>>? predicate = null)
        {
            var query = _context.DrawRecords.AsNoTracking();
            if (predicate != null) query = query.Where(predicate);
            return await query.ToListAsync();
        }

        public async Task<bool> AnyAsync(Expression<Func<DrawRecord, bool>> predicate)
        {
            return await _context.DrawRecords.AnyAsync(predicate);
        }

        public async Task CreateRangeAsync(IEnumerable<DrawRecord> draws)
        {
            await _context.DrawRecords.AddRangeAsync(draws);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
        }

        public async Task UpdateAsync(DrawRecord draw)
        {
            var tracked = await GetTrackedDrawAsync(draw.Id);
            ApplyEditableValues(tracked, draw);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRangeAsync(IEnumerable<DrawRecord> draws)
        {
            var disconnectedDraws = draws.ToList();
            var drawIds = disconnectedDraws.Select(draw => draw.Id).ToList();
            var trackedDraws = await _context.DrawRecords
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

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
        }

        public async Task DeleteAsync(Guid id)
        {
            await _context.DrawRecords
                .Where(d => d.Id == id)
                .ExecuteDeleteAsync();
        }

        public async Task DeleteRangeAsync(Expression<Func<DrawRecord, bool>> predicate)
        {
            await _context.DrawRecords
                .Where(predicate)
                .ExecuteDeleteAsync();
        }

        private async Task<DrawRecord> GetTrackedDrawAsync(Guid id)
        {
            return await _context.DrawRecords.SingleOrDefaultAsync(draw => draw.Id == id)
                ?? throw new InvalidOperationException($"No existe el sorteo con identificador '{id}'.");
        }

        private static void ApplyEditableValues(DrawRecord target, DrawRecord source)
        {
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
