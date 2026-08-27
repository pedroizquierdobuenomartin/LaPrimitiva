using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Exceptions;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LaPrimitiva.Infrastructure.Repositories
{
    public class WinningDrawRepository(IDbContextFactory<PrimitivaDbContext> contextFactory) : IWinningDrawRepository
    {
        private readonly IDbContextFactory<PrimitivaDbContext> _contextFactory = contextFactory;

        public async Task<List<WinningDraw>> GetListAsync(Expression<Func<WinningDraw, bool>>? predicate = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.WinningDraws.AsNoTracking();
            if (predicate != null) query = query.Where(predicate);
            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => query.OrderByDescending(d => d.DrawDate).ToListAsync(),
                "WinningDraw.List");
        }

        public async Task<List<int>> GetYearsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.WinningDraws
                    .AsNoTracking()
                    .Select(d => d.DrawDate.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync(),
                "WinningDraw.ListYears");
        }

        public async Task<DateTime?> GetLatestDateAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.WinningDraws.MaxAsync(d => (DateTime?)d.DrawDate),
                "WinningDraw.GetLatestDate");
        }

        public async Task<WinningDraw?> GetAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.WinningDraws.AsNoTracking().SingleOrDefaultAsync(draw => draw.Id == id),
                "WinningDraw.Get",
                id);
        }

        public async Task<bool> AnyAsync(Expression<Func<WinningDraw, bool>> predicate)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.WinningDraws.AnyAsync(predicate),
                "WinningDraw.Exists");
        }

        public async Task CreateAsync(WinningDraw draw)
        {
            draw.Validate();
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.WinningDraws.AddAsync(draw);
            await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.SaveChangesAsync(),
                "WinningDraw.Create",
                draw.Id);
        }

        public async Task UpdateAsync(WinningDraw draw)
        {
            draw.Validate();
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existing = await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.WinningDraws.SingleOrDefaultAsync(entity => entity.Id == draw.Id),
                "WinningDraw.GetForUpdate",
                draw.Id)
                ?? throw new ConcurrencyConflictException(draw.Id);
            context.Entry(existing).Property(entity => entity.RowVersion).OriginalValue = draw.RowVersion;
            existing.DrawDate = draw.DrawDate;
            existing.Number1 = draw.Number1;
            existing.Number2 = draw.Number2;
            existing.Number3 = draw.Number3;
            existing.Number4 = draw.Number4;
            existing.Number5 = draw.Number5;
            existing.Number6 = draw.Number6;
            existing.Complementario = draw.Complementario;
            existing.Reintegro = draw.Reintegro;
            existing.Joker = draw.Joker;

            await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.SaveChangesAsync(),
                "WinningDraw.Update",
                draw.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await PersistenceExceptionTranslator.ExecuteAsync(
                () => context.WinningDraws
                    .Where(d => d.Id == id)
                    .ExecuteDeleteAsync(),
                "WinningDraw.Delete",
                id);
        }
    }
}
