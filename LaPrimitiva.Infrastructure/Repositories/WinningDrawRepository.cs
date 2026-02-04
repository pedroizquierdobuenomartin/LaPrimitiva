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
    public class WinningDrawRepository(PrimitivaDbContext context) : IWinningDrawRepository
    {
        private readonly PrimitivaDbContext _context = context;

        public async Task<List<WinningDraw>> GetListAsync(Expression<Func<WinningDraw, bool>>? predicate = null)
        {
            var query = _context.WinningDraws.AsNoTracking();
            if (predicate != null) query = query.Where(predicate);
            return await query.OrderByDescending(d => d.DrawDate).ToListAsync();
        }

        public async Task<List<int>> GetYearsAsync()
        {
            return await _context.WinningDraws
                .Select(d => d.DrawDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
        }

        public async Task<WinningDraw?> GetAsync(Guid id)
        {
            return await _context.WinningDraws.FindAsync(id);
        }

        public async Task<bool> AnyAsync(Expression<Func<WinningDraw, bool>> predicate)
        {
            return await _context.WinningDraws.AnyAsync(predicate);
        }

        public async Task CreateAsync(WinningDraw draw)
        {
            await _context.WinningDraws.AddAsync(draw);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(WinningDraw draw)
        {
            var tracked = _context.WinningDraws.Local.FirstOrDefault(e => e.Id == draw.Id);
            if (tracked != null)
            {
                _context.Entry(tracked).State = EntityState.Detached;
            }

            _context.WinningDraws.Update(draw);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            await _context.WinningDraws
                .Where(d => d.Id == id)
                .ExecuteDeleteAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
