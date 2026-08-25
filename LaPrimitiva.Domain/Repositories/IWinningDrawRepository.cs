using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Domain.Repositories
{
    public interface IWinningDrawRepository
    {
        Task<List<WinningDraw>> GetListAsync(Expression<Func<WinningDraw, bool>>? predicate = null);
        Task<List<int>> GetYearsAsync();
        Task<WinningDraw?> GetAsync(Guid id);
        Task<DateTime?> GetLatestDateAsync();
        Task<bool> AnyAsync(Expression<Func<WinningDraw, bool>> predicate);
        Task CreateAsync(WinningDraw draw);
        Task UpdateAsync(WinningDraw draw);
        Task DeleteAsync(Guid id);
    }
}
