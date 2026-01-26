using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using LaPrimitiva.Application.DTOs;

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

        public async Task<List<PlanDto>> GetPlansByYearAsync(int year)
        {
            var startOfYear = new DateTime(year, 1, 1);
            var endOfYear = new DateTime(year, 12, 31, 23, 59, 59);

            return await _context.Plans
                .Where(p => p.EffectiveFrom <= endOfYear && (p.EffectiveTo == null || p.EffectiveTo >= startOfYear))
                .OrderByDescending(p => p.EffectiveFrom)
                .Select(p => new PlanDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    EffectiveFrom = p.EffectiveFrom,
                    EffectiveTo = p.EffectiveTo,
                    CostPerBet = p.CostPerBet,
                    BetsPerDraw = p.BetsPerDraw,
                    EnableJoker = p.EnableJoker,
                    JokerCostPerBet = p.JokerCostPerBet,
                    FixedCombinationLabel = p.FixedCombinationLabel,
                    CreatedAt = p.CreatedAt,
                    HasDraws = p.Draws.Any()
                })
                .ToListAsync();
        }

        public async Task<Plan?> GetPlanByIdAsync(Guid id)
        {
            return await _context.Plans.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task CreatePlanAsync(Plan plan)
        {
            // Overlap validation
            var overlap = await _context.Plans.AnyAsync(p => 
                (plan.EffectiveTo == null || p.EffectiveFrom <= plan.EffectiveTo) && 
                (p.EffectiveTo == null || p.EffectiveTo >= plan.EffectiveFrom));

            if (overlap)
            {
                throw new InvalidOperationException("Ya existe un plan que se solapa con este periodo de fechas.");
            }

            _context.Plans.Add(plan);
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePlanAsync(Plan plan)
        {
            var overlap = await _context.Plans.AnyAsync(p => 
                p.Id != plan.Id &&
                (plan.EffectiveTo == null || p.EffectiveFrom <= plan.EffectiveTo) && 
                (p.EffectiveTo == null || p.EffectiveTo >= plan.EffectiveFrom));

            if (overlap)
            {
                throw new InvalidOperationException("Las nuevas fechas se solapan con otro plan existente.");
            }

            plan.UpdatedAt = DateTime.UtcNow;
            _context.Entry(plan).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task DeletePlanAsync(Guid id)
        {
            var hasDraws = await _context.DrawRecords.AnyAsync(d => d.PlanId == id);
            if (hasDraws)
            {
                throw new InvalidOperationException("No se puede borrar un plan que ya tiene sorteos asociados.");
            }

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
