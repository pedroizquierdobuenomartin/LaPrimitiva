using System;
using System.Collections.Generic;
using System.Linq;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Application.Services
{
    /// <summary>
    /// Servicio encargado de la lógica de generación de sorteos para un rango de fechas.
    /// </summary>
    public class DrawGenerationService
    {
        /// <summary>
        /// Genera una lista de sorteos (Lunes, Jueves, Sábado) para un rango de fechas.
        /// </summary>
        public List<DrawRecord> GenerateDrawsForRange(Guid planId, DateTime from, DateTime to)
        {
            var draws = new List<DrawRecord>();
            var current = from.Date;

            // Find first Monday to start week numbering
            DateTime firstMonday = from;
            while (firstMonday.DayOfWeek != DayOfWeek.Monday)
            {
                firstMonday = firstMonday.AddDays(-1);
            }

            while (current <= to)
            {
                if (current.DayOfWeek == DayOfWeek.Monday || 
                    current.DayOfWeek == DayOfWeek.Thursday || 
                    current.DayOfWeek == DayOfWeek.Saturday)
                {
                    int weekNumber = ((current - firstMonday).Days / 7) + 1;
                    draws.Add(new DrawRecord
                    {
                        PlanId = planId,
                        DrawDate = current,
                        DrawType = MapDayToDrawType(current.DayOfWeek),
                        WeekNumber = weekNumber,
                        Played = false
                    });
                }
                current = current.AddDays(1);
            }

            return draws;
        }

        private DrawType MapDayToDrawType(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => DrawType.Lunes,
                DayOfWeek.Thursday => DrawType.Jueves,
                DayOfWeek.Saturday => DrawType.Sabado,
                _ => throw new ArgumentException("Invalid day for Primitiva draw")
            };
        }
    }
}
