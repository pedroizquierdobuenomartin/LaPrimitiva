using System.Threading.Tasks;
using LaPrimitiva.Application.DTOs;

namespace LaPrimitiva.Application.Interfaces
{
    public interface IAutomatedCombinationService
    {
        Task<CombinationResult> GenerateCombinationAsync(int variation = 0);

        Task<AutomatedCombinationBacktestResult> BacktestAsync(
            int minimumTrainingDraws = 104,
            double halfLifeDays = 365.0,
            double alpha = 1.0);
    }
}
