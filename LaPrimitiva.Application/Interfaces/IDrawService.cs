using System;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Application.Interfaces
{
    public interface IDrawService
    {
        Task<IReadOnlyList<Plan>> GetPlansByYearAsync(int year);
        Task<IReadOnlyList<DrawRecord>> GetDrawsByYearAsync(int year, Guid? planId = null);
        Task<IReadOnlyList<DrawRecord>> GetDrawsForWeekAsync(int year, int weekNumber);
        Task<Plan?> GetPlanAsync(Guid id);
        int GetCurrentWeekNumber(int year, DateTime currentDate);
        DrawRecord CreateDrawTemplate(Plan plan, int year, int weekNumber, DayOfWeek day);
        void RecalculateFinancials(DrawRecord draw, bool refreshCostsFromPlan = false);
        Task SaveDrawAsync(DrawRecord draw);
        Task SaveDrawsAsync(IEnumerable<DrawRecord> draws);
        Task DeleteWeeklyDrawAsync(int weekNumber, int year, Guid planId);
        Task ValidateDrawAsync(Guid planId, DateTime drawDate, Guid? currentDrawId = null);
    }
}
