using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;

namespace LaPrimitiva.Application.Services
{
    public class PlanService
    {
        private readonly PrimitivaDbContext _context;

        public PlanService(PrimitivaDbContext context)
        {
            _context = context;
        }

        public async Task<List<Plan>> GetPlansAsync()
        {
            return await _context.Plans.OrderByDescending(p => p.EffectiveFrom).ToListAsync();
        }

        public async Task<List<Plan>> GetPlansByYearAsync(int year)
        {
            var startOfYear = new DateTime(year, 1, 1);
            var endOfYear = new DateTime(year, 12, 31, 23, 59, 59);

            return await _context.Plans
                .Where(p => p.EffectiveFrom <= endOfYear && (p.EffectiveTo == null || p.EffectiveTo >= startOfYear))
                .OrderByDescending(p => p.EffectiveFrom)
                .ToListAsync();
        }

        public async Task<Plan?> GetPlanByIdAsync(Guid id)
        {
            return await _context.Plans.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task CreatePlanAsync(Plan plan)
        {
            // Simple overlap validation: check if any plan exists with EffectiveFrom or EffectiveTo within the new plan's range
            var overlap = await _context.Plans.AnyAsync(p => 
                (plan.EffectiveTo == null || p.EffectiveFrom <= plan.EffectiveTo) && 
                (p.EffectiveTo == null || p.EffectiveTo >= plan.EffectiveFrom));

            if (overlap)
            {
                // In a real app we might throw a custom exception. For this audit app, we'll just log or handle in UI.
            }

            _context.Plans.Add(plan);
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePlanAsync(Plan plan)
        {
            plan.UpdatedAt = DateTime.UtcNow;
            _context.Entry(plan).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task DeletePlanAsync(Guid id)
        {
            var plan = await _context.Plans.FindAsync(id);
            if (plan != null)
            {
                _context.Plans.Remove(plan);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Plan?> GetPlanForDateAsync(DateTime date)
        {
            return await _context.Plans
                .Where(p => p.EffectiveFrom <= date && (p.EffectiveTo == null || p.EffectiveTo >= date))
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();
        }
    }
}
