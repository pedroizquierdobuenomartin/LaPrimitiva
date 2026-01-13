using Xunit;
using LaPrimitiva.Domain.Entities;
using System;

namespace LaPrimitiva.Tests
{
    public class DrawRecordTests
    {
        [Fact]
        public void CalculatedTotalCost_CorrectlyCalculates_WithJoker()
        {
            // Arrange
            var plan = new Plan
            {
                CostPerBet = 1.00m,
                BetsPerDraw = 2,
                EnableJoker = true,
                JokerCostPerBet = 1.00m
            };

            var draw = new DrawRecord
            {
                Plan = plan,
                Played = true
            };

            // Act
            var totalCost = draw.CalculatedTotalCost;

            // Assert
            // (1.00 * 1) [Fixed] + (1.00 * 1) [Auto] + (1.00 * 1) [JokerFixed] + (1.00 * 1) [JokerAuto] ??
            // Let's check my formula:
            // FixedCost = Played ? Plan.CostPerBet : 0 => 1.00
            // AutoCost = Played ? Plan.CostPerBet : 0 => 1.00
            // JokerFixedCost = Played && Plan.EnableJoker ? Plan.JokerCostPerBet : 0 => 1.00
            // JokerAutoCost = Played && Plan.EnableJoker ? Plan.JokerCostPerBet : 0 => 1.00
            // Total = 4.00
            Assert.Equal(4.00m, totalCost);
        }

        [Fact]
        public void NetResult_CorrectlyCalculates()
        {
            // Arrange
            var plan = new Plan { CostPerBet = 1.0m, BetsPerDraw = 2, EnableJoker = false };
            var draw = new DrawRecord
            {
                Plan = plan,
                Played = true,
                FixedPrize = 5.0m,
                AutoPrize = 0m
            };

            // Act: Cost = 1.0 + 1.0 = 2.0. Prize = 5.0. Net = 3.0.
            Assert.Equal(3.0m, draw.NetResult);
        }

        [Fact]
        public void NotPlayed_CostIsZero()
        {
            // Arrange
            var plan = new Plan { CostPerBet = 1.0m, BetsPerDraw = 2, EnableJoker = true, JokerCostPerBet = 1.0m };
            var draw = new DrawRecord
            {
                Plan = plan,
                Played = false,
                FixedPrize = 100m // Should ignore prize if not played? No, prizes usually come from a play. 
                                  // But if Played=false, Cost must be 0.
            };

            Assert.Equal(0, draw.CalculatedTotalCost);
            Assert.Equal(100m, draw.NetResult);
        }
    }
}
