using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaPrimitiva.Application.DTOs;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Models;

namespace LaPrimitiva.Application.Interfaces
{
    public interface IWinningDrawService
    {
        Task<List<WinningDrawDto>> GetAllAsync(int? year = null);
        Task<List<int>> GetAvailableYearsAsync();
        Task<WinningDrawDto?> GetByIdAsync(Guid id);
        Task<DateTime?> GetLatestDrawDateAsync();
        Task<Result<WinningDrawDto>> CreateAsync(WinningDrawDto dto);
        Task<Result> UpdateAsync(WinningDrawDto dto);
        Task<Result> DeleteAsync(Guid id);
        Task<Result<WinningDrawDto>> SaveFromRssAsync(RssDraw rssDraw);
    }
}
