using System;
using System.Collections.Generic;
using LaPrimitiva.Domain.Errors;

namespace LaPrimitiva.Domain.Entities
{
    public class Plan
    {
        public const int MinBetsPerDraw = 1;
        public const int MaxBetsPerDraw = 100;

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public int WeeksToTrackDefault { get; set; } = 52;
        public decimal CostPerBet { get; set; } = 1.00m;
        public int BetsPerDraw { get; set; } = 2; // Fixed + Auto
        public bool EnableJoker
        {
            get => field;
            set
            {
                field = value;
                if (!value)
                {
                    JokerCostPerBet = 0m;
                }
                else if (JokerCostPerBet == 0)
                {
                    JokerCostPerBet = 1.00m;
                }
            }
        }
        public decimal JokerCostPerBet { get; set; }
        public string? FixedCombinationLabel { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public byte[] RowVersion { get; set; } = [];

        // Navigation property
        public ICollection<DrawRecord> Draws { get; set; } = new List<DrawRecord>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new BusinessRuleException("plan.name.required", "El nombre del plan no puede estar vacío.");
            }

            if (EffectiveTo.HasValue && EffectiveFrom > EffectiveTo.Value)
            {
                throw new BusinessRuleException("plan.period.invalid", "La fecha final no puede ser anterior a la fecha inicial.");
            }

            if (WeeksToTrackDefault < 0)
            {
                throw new BusinessRuleException("plan.weeks.negative", "El número de semanas a controlar no puede ser negativo.");
            }

            if (CostPerBet < 0)
            {
                throw new BusinessRuleException("plan.bet-cost.negative", "El coste por apuesta no puede ser negativo.");
            }

            if (BetsPerDraw is < MinBetsPerDraw or > MaxBetsPerDraw)
            {
                throw new BusinessRuleException(
                    "plan.bets-per-draw.range",
                    $"Las apuestas por sorteo deben estar entre {MinBetsPerDraw} y {MaxBetsPerDraw}.");
            }

            if (JokerCostPerBet < 0)
            {
                throw new BusinessRuleException("plan.joker-cost.negative", "El coste de Joker no puede ser negativo.");
            }

            if (!EnableJoker && JokerCostPerBet != 0)
            {
                throw new BusinessRuleException(
                    "plan.joker.disabled-cost",
                    "Un plan con Joker desactivado debe tener coste de Joker cero.");
            }
        }
    }
}
