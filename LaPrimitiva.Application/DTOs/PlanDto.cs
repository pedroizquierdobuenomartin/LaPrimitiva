using System;

namespace LaPrimitiva.Application.DTOs
{
    public record PlanDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public DateTime EffectiveFrom { get; init; }
        public DateTime? EffectiveTo { get; init; }
        public decimal CostPerBet { get; init; }
        public int BetsPerDraw { get; init; }
        public bool EnableJoker { get; init; }
        public decimal JokerCostPerBet { get; init; }
        public string? FixedCombinationLabel { get; init; }
        public DateTime CreatedAt { get; init; }
        public byte[] RowVersion { get; init; } = [];
        public bool HasDraws { get; init; }
        public bool HasOverlap { get; init; }
        public int TotalDraws { get; init; }
        public int ActiveDraws { get; init; }
        public int InactiveDraws { get; init; }

        public decimal TotalInvested { get; init; }
        public decimal TotalPrizesAmount { get; init; }
        public int WinningBetsCount { get; init; }
        public decimal NetBalance => TotalPrizesAmount - TotalInvested;
    }
}
