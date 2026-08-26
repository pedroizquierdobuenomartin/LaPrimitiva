using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Application.Interfaces;

public interface IDataExportService
{
    Task<IReadOnlyList<DrawRecord>> GetAllDrawsAsync();
}
