using System;
using LaPrimitiva.Domain.Services;

namespace LaPrimitiva.Domain.Entities
{
    public class DrawRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PlanId { get; set; }
        public Plan Plan { get; set; } = null!;

        public DrawType DrawType { get; set; }
        public DateTime DrawDate { get; set; }
        public int WeekNumber { get; set; }
        public bool Played { get; set; }

        public decimal FixedPrize { get; set; }
        public decimal AutoPrize { get; set; }
        public decimal JokerFixedPrize { get; set; }
        public decimal JokerAutoPrize { get; set; }

        public string? Notes { get; set; }

        // Audit Persistence Fields (Stored in DB)
        public decimal CosteFija { get; set; }
        public decimal CosteAuto { get; set; }
        public decimal CosteJokerFija { get; set; }
        public decimal CosteJokerAuto { get; set; }
        public decimal TotalCoste { get; set; }
        public decimal TotalPremios { get; set; }
        public decimal Neto { get; set; }
        public decimal Acumulado { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public byte[] RowVersion { get; set; } = [];

        // Plan-derived helpers are only used to snapshot the configured costs.
        public decimal FixedCost => Played ? Plan.CostPerBet : 0;
        public decimal AutoCost => Played ? Plan.CostPerBet * (Plan.BetsPerDraw - 1) : 0;
        public decimal JokerFixedCost => Played && Plan.EnableJoker ? Plan.JokerCostPerBet : 0;
        public decimal JokerAutoCost => Played && Plan.EnableJoker
            ? Plan.JokerCostPerBet * (Plan.BetsPerDraw - 1)
            : 0;
        
        /// <summary>
        /// Total cost is the sum of the four persisted cost components, including Joker.
        /// Total prizes follows the same rule and net is always prizes minus cost.
        /// </summary>
        public decimal CalculatedTotalCost => CosteFija + CosteAuto + CosteJokerFija + CosteJokerAuto;
        public decimal CalculatedTotalPrize => FixedPrize + AutoPrize + JokerFixedPrize + JokerAutoPrize;
        public decimal CalculatedNetResult => FinancialMetrics.CalculateNet(CalculatedTotalCost, CalculatedTotalPrize);

        public void RecalculateFinancials(bool refreshCostsFromPlan = false)
        {
            if (!Played)
            {
                CosteFija = 0;
                CosteAuto = 0;
                CosteJokerFija = 0;
                CosteJokerAuto = 0;
                FixedPrize = 0;
                AutoPrize = 0;
                JokerFixedPrize = 0;
                JokerAutoPrize = 0;
            }
            else if (refreshCostsFromPlan)
            {
                if (Plan is null)
                {
                    throw new InvalidOperationException("No se pueden actualizar los costes sin un plan asociado.");
                }

                Plan.Validate();
                CosteFija = FixedCost;
                CosteAuto = AutoCost;
                CosteJokerFija = JokerFixedCost;
                CosteJokerAuto = JokerAutoCost;

                if (!Plan.EnableJoker)
                {
                    JokerFixedPrize = 0;
                    JokerAutoPrize = 0;
                }
            }

            TotalCoste = CalculatedTotalCost;
            TotalPremios = CalculatedTotalPrize;
            Neto = CalculatedNetResult;
        }

        public decimal TotalPrize => CalculatedTotalPrize;
        public decimal NetResult => CalculatedNetResult;
    }
}
