using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Errors;
using LaPrimitiva.Domain.Exceptions;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LaPrimitiva.Infrastructure.Repositories
{
    /// <summary>
    /// Implementación de IPlanRepository utilizando EF Core 10.
    /// </summary>
    public class PlanRepository(IDbContextFactory<PrimitivaDbContext> contextFactory) : IPlanRepository
    {
        private readonly IDbContextFactory<PrimitivaDbContext> _contextFactory = contextFactory;

        public async Task<List<Plan>> GetListAsync(bool includeDraws = false)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.Plans.AsNoTracking();
            if (includeDraws) query = query.Include(p => p.Draws);
            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => query.OrderByDescending(p => p.EffectiveFrom).ToListAsync(),
                "Plan.List");
        }

        public async Task<List<Plan>> GetByYearAsync(int year)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var startOfYear = new DateTime(year, 1, 1);
            var endOfYear = new DateTime(year, 12, 31, 23, 59, 59);

            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.Plans.AsNoTracking()
                    .Include(p => p.Draws)
                    .Where(p => p.EffectiveFrom <= endOfYear && (p.EffectiveTo == null || p.EffectiveTo >= startOfYear))
                    .OrderByDescending(p => p.EffectiveFrom)
                    .ToListAsync(),
                "Plan.ListByYear");
        }

        public async Task<Plan?> GetAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id),
                "Plan.Get",
                id);
        }

        public async Task<Plan?> GetForDateAsync(DateTime date)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.Plans.AsNoTracking()
                    .Where(p => p.EffectiveFrom <= date && (p.EffectiveTo == null || p.EffectiveTo >= date))
                    .OrderByDescending(p => p.EffectiveFrom)
                    .FirstOrDefaultAsync(),
                "Plan.GetForDate");
        }

        public async Task<bool> AnyAsync(Expression<Func<Plan, bool>> predicate)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.Plans.AnyAsync(predicate),
                "Plan.Exists");
        }

        public async Task CreateAsync(Plan plan)
        {
            plan.Validate();
            await using var context = await _contextFactory.CreateDbContextAsync();
            await EnsureNoOverlapAsync(context, plan);
            context.Plans.Add(plan);
            await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.SaveChangesAsync(),
                "Plan.Create",
                plan.Id);
        }

        public async Task UpdateAsync(Plan plan)
        {
            plan.Validate();
            await using var context = await _contextFactory.CreateDbContextAsync();
            await EnsureNoOverlapAsync(context, plan);

            var existing = await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.Plans.SingleOrDefaultAsync(existing => existing.Id == plan.Id),
                "Plan.GetForUpdate",
                plan.Id)
                ?? throw new ConcurrencyConflictException(plan.Id);
            context.Entry(existing).Property(entity => entity.RowVersion).OriginalValue = plan.RowVersion;

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

            await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.SaveChangesAsync(),
                "Plan.Update",
                plan.Id);
        }

        private static async Task EnsureNoOverlapAsync(PrimitivaDbContext context, Plan plan)
        {
            var overlap = await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.Plans.AnyAsync(existing =>
                    existing.Id != plan.Id &&
                    (plan.EffectiveTo == null || existing.EffectiveFrom <= plan.EffectiveTo) &&
                    (existing.EffectiveTo == null || existing.EffectiveTo >= plan.EffectiveFrom)),
                "Plan.ValidateOverlap",
                plan.Id);

            if (overlap)
            {
                throw new DataIntegrityException(
                    "plan.period.overlap",
                    "Ya existe un plan que se solapa con este periodo de fechas.");
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            // Uso de ExecuteDeleteAsync para eficiencia si no hay lógica compleja, 
            // pero aquí validamos antes en el Application Service.
            await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.Plans
                    .Where(p => p.Id == id)
                    .ExecuteDeleteAsync(),
                "Plan.Delete",
                id);
        }
    }
}
