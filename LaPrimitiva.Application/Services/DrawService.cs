using System;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Application.Interfaces;

namespace LaPrimitiva.Application.Services
{
    /// <summary>
    /// Servicio para operaciones relacionadas con los sorteos registrados.
    /// </summary>
    public class DrawService(IDrawRepository drawRepository, IPlanRepository planRepository) : IDrawService
    {
        private readonly IDrawRepository _drawRepository = drawRepository;
        private readonly IPlanRepository _planRepository = planRepository;

        /// <summary>
        /// Elimina los sorteos de una semana completa para un plan y año específicos.
        /// </summary>
        public async Task DeleteWeeklyDrawAsync(int weekNumber, int year, Guid planId)
        {
            await _drawRepository.DeleteRangeAsync(d => 
                d.WeekNumber == weekNumber && 
                d.DrawDate.Year == year && 
                d.PlanId == planId);
        }

        /// <summary>
        /// Valida que el sorteo no esté duplicado para la misma fecha (excepto el mismo ID si es edición)
        /// y que esté dentro del periodo de vigencia del plan.
        /// </summary>
        public async Task ValidateDrawAsync(Guid planId, DateTime drawDate, Guid? currentDrawId = null)
        {
            // 1. Validar que no exista otro sorteo para la misma fecha (excluyendo el actual si es edición)
            var duplicate = await _drawRepository.AnyAsync(d => 
                d.DrawDate.Date == drawDate.Date && d.Id != currentDrawId);
            
            if (duplicate)
            {
                throw new InvalidOperationException($"Ya existe un sorteo registrado para la fecha {drawDate:dd/MM/yyyy}.");
            }

            // 2. Validar que la fecha esté dentro del rango del plan
            var plan = await _planRepository.GetAsync(planId);
            if (plan == null)
            {
                throw new InvalidOperationException("El plan seleccionado no existe.");
            }

            if (drawDate.Date < plan.EffectiveFrom.Date || (plan.EffectiveTo.HasValue && drawDate.Date > plan.EffectiveTo.Value.Date))
            {
                var periodStr = plan.EffectiveTo.HasValue 
                    ? $"{plan.EffectiveFrom:dd/MM/yyyy} - {plan.EffectiveTo:dd/MM/yyyy}"
                    : $"desde {plan.EffectiveFrom:dd/MM/yyyy}";
                
                throw new InvalidOperationException($"La fecha {drawDate:dd/MM/yyyy} está fuera del periodo del plan ({periodStr}).");
            }
        }
    }
}
