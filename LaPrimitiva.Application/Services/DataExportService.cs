using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;

namespace LaPrimitiva.Application.Services;

public sealed class DataExportService(IDrawRepository drawRepository) : IDataExportService
{
    public async Task<IReadOnlyList<DrawRecord>> GetAllDrawsAsync()
    {
        var draws = await drawRepository.GetListAsync();
        return draws.OrderBy(draw => draw.DrawDate).ToList();
    }
}
