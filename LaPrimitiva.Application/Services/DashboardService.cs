using LaPrimitiva.Application.DTOs;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Domain.Repositories;

namespace LaPrimitiva.Application.Services;

public sealed class DashboardService(
    IDrawRepository drawRepository,
    SummaryService summaryService) : IDashboardService
{
    public async Task<DashboardDto> GetDashboardAsync(int? year = null)
    {
        var draws = year.HasValue
            ? await drawRepository.GetListAsync(draw => draw.DrawDate.Year == year.Value)
            : await drawRepository.GetListAsync();

        return new DashboardDto(
            summaryService.GetSummary(draws),
            summaryService.GetMonthlySummaries(draws));
    }
}
