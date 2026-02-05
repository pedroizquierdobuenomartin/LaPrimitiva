using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Application.DTOs;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Domain.Models;

namespace LaPrimitiva.Application.Services
{
    public class WinningDrawService(IWinningDrawRepository repository) : IWinningDrawService
    {
        private readonly IWinningDrawRepository _repository = repository;

        public async Task<List<WinningDrawDto>> GetAllAsync(int? year = null)
        {
            var draws = await _repository.GetListAsync(d => !year.HasValue || d.DrawDate.Year == year.Value);
            return draws.Select(MapToDto).ToList();
        }

        public async Task<List<int>> GetAvailableYearsAsync()
        {
            return await _repository.GetYearsAsync();
        }

        public async Task<DateTime?> GetLatestDrawDateAsync()
        {
            return await _repository.GetLatestDateAsync();
        }

        public async Task<WinningDrawDto?> GetByIdAsync(Guid id)
        {
            var draw = await _repository.GetAsync(id);
            return draw == null ? null : MapToDto(draw);
        }

        public async Task<Result<WinningDrawDto>> CreateAsync(WinningDrawDto dto)
        {
            var validation = ValidateUniqueNumbers(dto);
            if (!validation.IsSuccess) return Result<WinningDrawDto>.Failure(validation.Error!);

            var exists = await _repository.AnyAsync(d => d.DrawDate.Date == dto.DrawDate.Date);
            if (exists)
            {
                return Result<WinningDrawDto>.Failure("Ya existe un sorteo para la fecha especificada.");
            }

            var entity = MapToEntity(dto);
            await _repository.CreateAsync(entity);
            
            return Result<WinningDrawDto>.Success(MapToDto(entity));
        }

        public async Task<Result> UpdateAsync(WinningDrawDto dto)
        {
            var validation = ValidateUniqueNumbers(dto);
            if (!validation.IsSuccess) return validation;

            var exists = await _repository.AnyAsync(d => d.DrawDate.Date == dto.DrawDate.Date && d.Id != dto.Id);
            if (exists)
            {
                return Result.Failure("Ya existe otro sorteo para la fecha especificada.");
            }

            var entity = MapToEntity(dto);
            await _repository.UpdateAsync(entity);
            
            return Result.Success();
        }

        private static Result ValidateUniqueNumbers(WinningDrawDto dto)
        {
            var numbers = new[] 
            { 
                dto.Number1, dto.Number2, dto.Number3, 
                dto.Number4, dto.Number5, dto.Number6, 
                dto.Complementario 
            };

            var duplicates = numbers.Where(n => n > 0).GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Any())
            {
                return Result.Failure($"Los números ganadores y el complementario no se pueden repetir. Duplicados: {string.Join(", ", duplicates)}");
            }

            return Result.Success();
        }

        public async Task<Result> DeleteAsync(Guid id)
        {
            await _repository.DeleteAsync(id);
            return Result.Success();
        }

        public async Task<Result<WinningDrawDto>> SaveFromRssAsync(RssDraw rssDraw)
        {
            var dto = new WinningDrawDto
            {
                DrawDate = rssDraw.Date,
                Number1 = rssDraw.Numbers[0],
                Number2 = rssDraw.Numbers[1],
                Number3 = rssDraw.Numbers[2],
                Number4 = rssDraw.Numbers[3],
                Number5 = rssDraw.Numbers[4],
                Number6 = rssDraw.Numbers[5],
                Complementario = rssDraw.Complementary,
                Reintegro = rssDraw.Reintegro,
                Joker = rssDraw.Joker?.ToString()
            };

            return await CreateAsync(dto);
        }

        private static WinningDrawDto MapToDto(WinningDraw entity) => new(
            entity.Id,
            entity.DrawDate,
            entity.Number1,
            entity.Number2,
            entity.Number3,
            entity.Number4,
            entity.Number5,
            entity.Number6,
            entity.Complementario,
            entity.Reintegro,
            entity.Joker
        );

        private static WinningDraw MapToEntity(WinningDrawDto dto)
        {
            var sortedNumbers = new[] 
            { 
                dto.Number1, dto.Number2, dto.Number3, 
                dto.Number4, dto.Number5, dto.Number6 
            }.OrderBy(n => n).ToArray();

            return new WinningDraw()
            {
                Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                DrawDate = dto.DrawDate,
                Number1 = sortedNumbers[0],
                Number2 = sortedNumbers[1],
                Number3 = sortedNumbers[2],
                Number4 = sortedNumbers[3],
                Number5 = sortedNumbers[4],
                Number6 = sortedNumbers[5],
                Complementario = dto.Complementario,
                Reintegro = dto.Reintegro,
                Joker = dto.Joker
            };
        }
    }
}
