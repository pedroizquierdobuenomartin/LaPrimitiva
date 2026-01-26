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
    public class DrawService(IDrawRepository drawRepository) : IDrawService
    {
        private readonly IDrawRepository _drawRepository = drawRepository;

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
    }
}
