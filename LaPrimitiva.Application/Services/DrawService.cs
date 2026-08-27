using System;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Errors;

namespace LaPrimitiva.Application.Services
{
    /// <summary>
    /// Servicio para operaciones relacionadas con los sorteos registrados.
    /// </summary>
    public class DrawService(IDrawRepository drawRepository, IPlanRepository planRepository) : IDrawService
    {
        private readonly IDrawRepository _drawRepository = drawRepository;
        private readonly IPlanRepository _planRepository = planRepository;

        public async Task<IReadOnlyList<Plan>> GetPlansByYearAsync(int year) =>
            await _planRepository.GetByYearAsync(year);

        public async Task<IReadOnlyList<DrawRecord>> GetDrawsByYearAsync(int year, Guid? planId = null)
        {
            var draws = await _drawRepository.GetListAsync(draw => draw.DrawDate.Year == year);
            return planId.HasValue
                ? draws.Where(draw => draw.PlanId == planId.Value).ToList()
                : draws;
        }

        public async Task<IReadOnlyList<DrawRecord>> GetDrawsForWeekAsync(int year, int weekNumber)
        {
            var draws = await _drawRepository.GetListAsync(draw =>
                draw.DrawDate.Year == year && draw.WeekNumber == weekNumber);
            var plans = await _planRepository.GetByYearAsync(year);
            var plansById = plans.ToDictionary(plan => plan.Id);

            foreach (var draw in draws)
            {
                if (plansById.TryGetValue(draw.PlanId, out var plan))
                {
                    draw.Plan = plan;
                }
            }

            return draws;
        }

        public Task<Plan?> GetPlanAsync(Guid id) => _planRepository.GetAsync(id);

        public int GetCurrentWeekNumber(int year, DateTime currentDate)
        {
            var referenceDate = currentDate.Year == year ? currentDate.Date : new DateTime(year, 1, 1);
            var firstMonday = GetFirstMonday(year);
            return referenceDate < firstMonday ? 1 : ((referenceDate - firstMonday).Days / 7) + 1;
        }

        public DrawRecord CreateDrawTemplate(Plan plan, int year, int weekNumber, DayOfWeek day)
        {
            var date = GetFirstMonday(year).AddDays((weekNumber - 1) * 7);
            while (date.DayOfWeek != day)
            {
                date = date.AddDays(1);
            }

            return new DrawRecord
            {
                Id = Guid.Empty,
                WeekNumber = weekNumber,
                DrawDate = date,
                DrawType = MapDrawType(day),
                PlanId = plan.Id,
                Plan = plan,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void RecalculateFinancials(DrawRecord draw, bool refreshCostsFromPlan = false) =>
            draw.RecalculateFinancials(refreshCostsFromPlan);

        public async Task SaveDrawAsync(DrawRecord draw)
        {
            draw.RecalculateFinancials();
            draw.UpdatedAt = DateTime.UtcNow;
            await _drawRepository.UpdateAsync(draw);
        }

        public async Task SaveDrawsAsync(IEnumerable<DrawRecord> draws)
        {
            var drawList = draws.ToList();
            foreach (var draw in drawList)
            {
                await ValidateDrawAsync(
                    draw.PlanId,
                    draw.DrawDate,
                    draw.Id != Guid.Empty ? draw.Id : null);
                draw.RecalculateFinancials();
                draw.UpdatedAt = DateTime.UtcNow;
                draw.Plan = null!;
            }

            var newDraws = drawList.Where(draw => draw.Id == Guid.Empty).ToList();
            var existingDraws = drawList.Where(draw => draw.Id != Guid.Empty).ToList();

            if (newDraws.Count > 0)
            {
                await _drawRepository.CreateRangeAsync(newDraws);
            }

            if (existingDraws.Count > 0)
            {
                await _drawRepository.UpdateRangeAsync(existingDraws);
            }
        }

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
                throw new DataIntegrityException(
                    "draw.date.duplicate",
                    $"Ya existe un sorteo registrado para la fecha {drawDate:dd/MM/yyyy}.",
                    new Dictionary<string, object?> { ["DrawDate"] = drawDate.Date });
            }

            // 2. Validar que la fecha esté dentro del rango del plan
            var plan = await _planRepository.GetAsync(planId);
            if (plan == null)
            {
                throw new EntityNotFoundException("Plan", planId);
            }

            if (drawDate.Date < plan.EffectiveFrom.Date || (plan.EffectiveTo.HasValue && drawDate.Date > plan.EffectiveTo.Value.Date))
            {
                var periodStr = plan.EffectiveTo.HasValue 
                    ? $"{plan.EffectiveFrom:dd/MM/yyyy} - {plan.EffectiveTo:dd/MM/yyyy}"
                    : $"desde {plan.EffectiveFrom:dd/MM/yyyy}";
                
                throw new BusinessRuleException(
                    "draw.date.outside-plan-period",
                    $"La fecha {drawDate:dd/MM/yyyy} está fuera del periodo del plan ({periodStr}).",
                    new Dictionary<string, object?>
                    {
                        ["DrawDate"] = drawDate.Date,
                        ["PlanId"] = planId
                    });
            }
        }

        private static DateTime GetFirstMonday(int year)
        {
            var date = new DateTime(year, 1, 1);
            while (date.DayOfWeek != DayOfWeek.Monday)
            {
                date = date.AddDays(1);
            }

            return date;
        }

        private static DrawType MapDrawType(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => DrawType.Lunes,
            DayOfWeek.Thursday => DrawType.Jueves,
            DayOfWeek.Saturday => DrawType.Sabado,
            _ => throw new ArgumentOutOfRangeException(
                nameof(day),
                day,
                "El día seleccionado no corresponde a un sorteo de La Primitiva.")
        };
    }
}
