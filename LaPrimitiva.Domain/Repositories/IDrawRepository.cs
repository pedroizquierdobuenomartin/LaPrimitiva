using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Domain.Repositories
{
    /// <summary>
    /// Interfaz para el repositorio de Registros de Sorteo.
    /// </summary>
    public interface IDrawRepository
    {
        Task<List<DrawRecord>> GetListAsync(Expression<Func<DrawRecord, bool>>? predicate = null);
        Task<bool> AnyAsync(Expression<Func<DrawRecord, bool>> predicate);
        Task CreateRangeAsync(IEnumerable<DrawRecord> draws);
        Task DeleteAsync(Guid id);
        Task DeleteRangeAsync(Expression<Func<DrawRecord, bool>> predicate);
        Task SaveChangesAsync();
    }
}
