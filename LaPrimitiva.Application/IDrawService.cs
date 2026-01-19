using System;
using System.Threading.Tasks;

namespace LaPrimitiva.Application.Services
{
    public interface IDrawService
    {
        Task DeleteWeeklyDrawAsync(int weekNumber, int year, Guid planId);
    }
}
