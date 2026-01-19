using System;

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

        // Visual helper for old calculations (can be used to pre-fill)
        public decimal FixedCost => Played ? Plan.CostPerBet : 0;
        public decimal AutoCost => Played ? Plan.CostPerBet : 0;
        public decimal JokerFixedCost => Played && Plan.EnableJoker ? Plan.JokerCostPerBet : 0;
        public decimal JokerAutoCost => Played && Plan.EnableJoker ? Plan.JokerCostPerBet : 0;
        
        public decimal CalculatedTotalCost => FixedCost + AutoCost + JokerFixedCost + JokerAutoCost;
        public decimal CalculatedTotalPrize => FixedPrize + AutoPrize + JokerFixedPrize + JokerAutoPrize;
        public decimal CalculatedNetResult => CalculatedTotalPrize - CalculatedTotalCost;

        // Compatibility aliases to avoid breaking existing UI immediately if possible, 
        // but we'll update the UI anyway.
        public decimal TotalPrize => CalculatedTotalPrize;
        public decimal NetResult => CalculatedNetResult;
    }
}
