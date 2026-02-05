using System.Threading.Tasks;
using LaPrimitiva.Application.DTOs;

namespace LaPrimitiva.Application.Interfaces
{
    public interface IAutomatedCombinationService
    {
        Task<CombinationResult> GenerateCombinationAsync(double halfLifeDays = 365.0, double alpha = 1.0);
    }
}
