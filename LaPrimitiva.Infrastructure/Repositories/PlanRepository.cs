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
            return await _context.Plans.FirstOrDefaultAsync(p => p.Id == id);
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
            _context.Plans.Add(plan);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Plan plan)
        {
            plan.UpdatedAt = DateTime.UtcNow;
            _context.Entry(plan).State = EntityState.Modified;
            await _context.SaveChangesAsync();
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
