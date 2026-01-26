using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Domain.Repositories
{
    /// <summary>
    /// Interfaz para el repositorio de Planes de sorteo.
    /// </summary>
    public interface IPlanRepository
    {
        Task<List<Plan>> GetListAsync(bool includeDraws = false);
        Task<List<Plan>> GetByYearAsync(int year);
        Task<Plan?> GetAsync(Guid id);
        Task<Plan?> GetForDateAsync(DateTime date);
        Task<bool> AnyAsync(Expression<Func<Plan, bool>> predicate);
        Task CreateAsync(Plan plan);
        Task UpdateAsync(Plan plan);
        Task DeleteAsync(Guid id);
    }
}
