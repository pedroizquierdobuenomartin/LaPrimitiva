using System;
using System.Collections.Generic;

namespace LaPrimitiva.Domain.Entities
{
    public class Plan
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public int WeeksToTrackDefault { get; set; } = 52;
        public decimal CostPerBet { get; set; } = 1.00m;
        public int BetsPerDraw { get; set; } = 2; // Fixed + Auto
        public bool EnableJoker { get; set; }
        public decimal JokerCostPerBet { get; set; } = 0.50m; // Usually 1.00 but requirement says default 0 or 1.00. I'll stick to requirement default.
        public string? FixedCombinationLabel { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<DrawRecord> Draws { get; set; } = new List<DrawRecord>();
    }
}
