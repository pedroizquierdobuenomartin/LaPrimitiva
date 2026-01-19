using System;
using System.Threading.Tasks;

namespace LaPrimitiva.Application.Interfaces
{
    public interface IDrawService
    {
        Task DeleteWeeklyDrawAsync(int weekNumber, int year, Guid planId);
    }
}
