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
    /// Implementación de IPlanRepository utilizando EF Core 10.
    /// </summary>
    public class PlanRepository(PrimitivaDbContext context) : IPlanRepository
    {
        private readonly PrimitivaDbContext _context = context;

        public async Task<List<Plan>> GetListAsync(bool includeDraws = false)
        {
            var query = _context.Plans.AsNoTracking();
            if (includeDraws) query = query.Include(p => p.Draws);
            return await query.OrderByDescending(p => p.EffectiveFrom).ToListAsync();
        }

        public async Task<List<Plan>> GetByYearAsync(int year)
        {
            var startOfYear = new DateTime(year, 1, 1);
            var endOfYear = new DateTime(year, 12, 31, 23, 59, 59);

            return await _context.Plans.AsNoTracking()
                .Include(p => p.Draws)
                .Where(p => p.EffectiveFrom <= endOfYear && (p.EffectiveTo == null || p.EffectiveTo >= startOfYear))
                .OrderByDescending(p => p.EffectiveFrom)
                .ToListAsync();
        }

        public async Task<Plan?> GetAsync(Guid id)
        {
            return await _context.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Plan?> GetForDateAsync(DateTime date)
        {
            return await _context.Plans.AsNoTracking()
                .Where(p => p.EffectiveFrom <= date && (p.EffectiveTo == null || p.EffectiveTo >= date))
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> AnyAsync(Expression<Func<Plan, bool>> predicate)
        {
            return await _context.Plans.AnyAsync(predicate);
        }

        public async Task CreateAsync(Plan plan)
        {
            plan.Validate();
            await EnsureNoOverlapAsync(plan);
            _context.Plans.Add(plan);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Plan plan)
        {
            plan.Validate();
            await EnsureNoOverlapAsync(plan);

            var existing = await _context.Plans.SingleOrDefaultAsync(existing => existing.Id == plan.Id)
                ?? throw new InvalidOperationException("No se ha encontrado el plan que se quiere actualizar.");

            existing.Name = plan.Name;
            existing.EffectiveFrom = plan.EffectiveFrom;
            existing.EffectiveTo = plan.EffectiveTo;
            existing.WeeksToTrackDefault = plan.WeeksToTrackDefault;
            existing.CostPerBet = plan.CostPerBet;
            existing.BetsPerDraw = plan.BetsPerDraw;
            existing.EnableJoker = plan.EnableJoker;
            existing.JokerCostPerBet = plan.JokerCostPerBet;
            existing.FixedCombinationLabel = plan.FixedCombinationLabel;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private async Task EnsureNoOverlapAsync(Plan plan)
        {
            var overlap = await _context.Plans.AnyAsync(existing =>
                existing.Id != plan.Id &&
                (plan.EffectiveTo == null || existing.EffectiveFrom <= plan.EffectiveTo) &&
                (existing.EffectiveTo == null || existing.EffectiveTo >= plan.EffectiveFrom));

            if (overlap)
            {
                throw new InvalidOperationException("Ya existe un plan que se solapa con este periodo de fechas.");
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            // Uso de ExecuteDeleteAsync para eficiencia si no hay lógica compleja, 
            // pero aquí validamos antes en el Application Service.
            await _context.Plans
                .Where(p => p.Id == id)
                .ExecuteDeleteAsync();
        }
    }
}
