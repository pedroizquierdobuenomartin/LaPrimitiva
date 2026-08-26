using LaPrimitiva.Application.DTOs;

namespace LaPrimitiva.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(int? year = null);
}
