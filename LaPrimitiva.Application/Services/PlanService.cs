using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Application.DTOs;

namespace LaPrimitiva.Application.Services
{
    /// <summary>
    /// Servicio para la gestión de planes de sorteo.
    /// </summary>
    public class PlanService(IPlanRepository planRepository, IDrawRepository drawRepository)
    {
        private readonly IPlanRepository _planRepository = planRepository;
        private readonly IDrawRepository _drawRepository = drawRepository;

        /// <summary>
        /// Obtiene la lista completa de planes.
        /// </summary>
        public async Task<List<PlanDto>> GetPlansAsync()
        {
            var plans = await _planRepository.GetListAsync(includeDraws: true);
            return plans.Select(MapToDto).ToList();
        }

        /// <summary>
        /// Obtiene los planes vigentes para un año específico.
        /// </summary>
        /// <param name="year">Año a consultar.</param>
        public async Task<List<PlanDto>> GetPlansByYearAsync(int year)
        {
            var plans = await _planRepository.GetByYearAsync(year);
            var dtos = plans.Select(MapToDto).ToList();
            
            // In-memory overlap detection for visual flagging
            for (int i = 0; i < dtos.Count; i++)
            {
                var current = dtos[i];
                var hasOverlap = dtos.Any(other => 
                    other.Id != current.Id &&
                    (current.EffectiveTo == null || other.EffectiveFrom <= current.EffectiveTo) &&
                    (other.EffectiveTo == null || other.EffectiveTo >= current.EffectiveFrom));
                
                if (hasOverlap)
                {
                    dtos[i] = current with { HasOverlap = true };
                }
            }
            
            return dtos;
        }

        /// <summary>
        /// Obtiene un plan por su identificador único.
        /// </summary>
        public async Task<PlanDto?> GetPlanByIdAsync(Guid id)
        {
            var plan = await _planRepository.GetAsync(id);
            return plan != null ? MapToDto(plan) : null;
        }

        /// <summary>
        /// Crea un nuevo plan de sorteo validando que no existan solapamientos de fechas.
        /// </summary>
        public async Task CreatePlanAsync(Plan plan)
        {
            // Validación de solapamiento
            var overlap = await _planRepository.AnyAsync(p => 
                (plan.EffectiveTo == null || p.EffectiveFrom <= plan.EffectiveTo) && 
                (p.EffectiveTo == null || p.EffectiveTo >= plan.EffectiveFrom));

            if (overlap)
            {
                throw new InvalidOperationException("Ya existe un plan que se solapa con este periodo de fechas.");
            }

            await _planRepository.CreateAsync(plan);
        }

        /// <summary>
        /// Actualiza un plan existente.
        /// </summary>
        public async Task UpdatePlanAsync(Plan plan)
        {
            var overlap = await _planRepository.AnyAsync(p => 
                p.Id != plan.Id &&
                (plan.EffectiveTo == null || p.EffectiveFrom <= plan.EffectiveTo) && 
                (p.EffectiveTo == null || p.EffectiveTo >= plan.EffectiveFrom));

            if (overlap)
            {
                throw new InvalidOperationException("Las nuevas fechas se solapan con otro plan existente.");
            }

            await _planRepository.UpdateAsync(plan);
        }

        /// <summary>
        /// Elimina un plan si no tiene sorteos asociados.
        /// </summary>
        public async Task DeletePlanAsync(Guid id)
        {
            var hasDraws = await _drawRepository.AnyAsync(d => d.PlanId == id);
            if (hasDraws)
            {
                throw new InvalidOperationException("No se puede borrar un plan que ya tiene sorteos asociados.");
            }

            await _planRepository.DeleteAsync(id);
        }

        /// <summary>
        /// Obtiene el plan vigente para una fecha concreta.
        /// </summary>
        public async Task<PlanDto?> GetPlanForDateAsync(DateTime date)
        {
            var plan = await _planRepository.GetForDateAsync(date);
            return plan != null ? MapToDto(plan) : null;
        }

        /// <summary>
        /// Obtiene la lista de años en los que existen planes activos.
        /// </summary>
        public async Task<List<int>> GetAvailableYearsAsync()
        {
            var plans = await _planRepository.GetListAsync();
            if (!plans.Any())
            {
                return [DateTime.Now.Year];
            }

            var years = new HashSet<int>();
            var currentYear = DateTime.Now.Year;
            var maxFutureYear = currentYear + 2;

            foreach (var plan in plans)
            {
                int startYear = plan.EffectiveFrom.Year;
                int endYear = plan.EffectiveTo?.Year ?? maxFutureYear;

                // Clamp future years for indefinite plans to avoid huge lists
                if (endYear > maxFutureYear + 5) endYear = maxFutureYear + 5;

                for (int y = startYear; y <= endYear; y++)
                {
                    years.Add(y);
                }
            }

            // Always ensure current year is available if it's within or near plan ranges
            years.Add(currentYear);

            return [.. years.OrderBy(y => y)];
        }

        private static PlanDto MapToDto(Plan p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            EffectiveFrom = p.EffectiveFrom,
            EffectiveTo = p.EffectiveTo,
            CostPerBet = p.CostPerBet,
            BetsPerDraw = p.BetsPerDraw,
            EnableJoker = p.EnableJoker,
            JokerCostPerBet = p.JokerCostPerBet,
            FixedCombinationLabel = p.FixedCombinationLabel,
            CreatedAt = p.CreatedAt,
            HasDraws = p.Draws != null && p.Draws.Any()
        };
    }
}
