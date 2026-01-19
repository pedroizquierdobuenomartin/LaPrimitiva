using System;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using LaPrimitiva.Application.Interfaces;

namespace LaPrimitiva.Application.Services
{
    public class DrawService(PrimitivaDbContext db) : IDrawService
    {
        public async Task DeleteWeeklyDrawAsync(int weekNumber, int year, Guid planId)
        {
            var draws = await db.DrawRecords
                .Where(d => d.WeekNumber == weekNumber && d.DrawDate.Year == year && d.PlanId == planId)
                .ToListAsync();

            if (draws.Any())
            {
                db.DrawRecords.RemoveRange(draws);
                await db.SaveChangesAsync();
            }
        }
    }
}
