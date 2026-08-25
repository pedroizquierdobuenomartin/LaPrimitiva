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
    public class WinningDrawRepository(IDbContextFactory<PrimitivaDbContext> contextFactory) : IWinningDrawRepository
    {
        private readonly IDbContextFactory<PrimitivaDbContext> _contextFactory = contextFactory;

        public async Task<List<WinningDraw>> GetListAsync(Expression<Func<WinningDraw, bool>>? predicate = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.WinningDraws.AsNoTracking();
            if (predicate != null) query = query.Where(predicate);
            return await query.OrderByDescending(d => d.DrawDate).ToListAsync();
        }

        public async Task<List<int>> GetYearsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.WinningDraws
                .AsNoTracking()
                .Select(d => d.DrawDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
        }

        public async Task<DateTime?> GetLatestDateAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.WinningDraws.MaxAsync(d => (DateTime?)d.DrawDate);
        }

        public async Task<WinningDraw?> GetAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.WinningDraws.AsNoTracking().SingleOrDefaultAsync(draw => draw.Id == id);
        }

        public async Task<bool> AnyAsync(Expression<Func<WinningDraw, bool>> predicate)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.WinningDraws.AnyAsync(predicate);
        }

        public async Task CreateAsync(WinningDraw draw)
        {
            draw.Validate();
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.WinningDraws.AddAsync(draw);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(WinningDraw draw)
        {
            draw.Validate();
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.WinningDraws.Update(draw);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.WinningDraws
                .Where(d => d.Id == id)
                .ExecuteDeleteAsync();
        }
    }
}
