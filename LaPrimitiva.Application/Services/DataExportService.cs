using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;

namespace LaPrimitiva.Application.Services;

public sealed class DataExportService(IDrawRepository drawRepository) : IDataExportService
{
    public async Task<IReadOnlyList<DrawRecord>> GetAllDrawsAsync()
    {
        var draws = await drawRepository.GetListAsync();
        var orderedDraws = draws.OrderBy(draw => draw.DrawDate).ToList();
        var accumulatedNetByPlanAndYear = new Dictionary<(Guid PlanId, int Year), decimal>();

        foreach (var draw in orderedDraws)
        {
            var key = (draw.PlanId, draw.DrawDate.Year);
            accumulatedNetByPlanAndYear.TryGetValue(key, out var accumulatedNet);
            accumulatedNet += draw.Neto;
            accumulatedNetByPlanAndYear[key] = accumulatedNet;
            draw.Acumulado = accumulatedNet;
        }

        return orderedDraws;
    }
}
