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

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
