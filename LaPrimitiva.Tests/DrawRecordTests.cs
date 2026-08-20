using Xunit;
using LaPrimitiva.Domain.Entities;
using System;

namespace LaPrimitiva.Tests
{
    public class DrawRecordTests
    {
        [Fact]
        public void RecalculateFinancials_IncludesJokerCostsAndPrizes_WhenEnabledAndAwarded()
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
                Played = true,
                FixedPrize = 5m,
                AutoPrize = 3m,
                JokerFixedPrize = 20m,
                JokerAutoPrize = 10m
            };

            draw.RecalculateFinancials(refreshCostsFromPlan: true);

            Assert.Equal(4m, draw.TotalCoste);
            Assert.Equal(38m, draw.TotalPremios);
            Assert.Equal(34m, draw.Neto);
            Assert.Equal(draw.CosteFija + draw.CosteAuto + draw.CosteJokerFija + draw.CosteJokerAuto, draw.TotalCoste);
            Assert.Equal(draw.FixedPrize + draw.AutoPrize + draw.JokerFixedPrize + draw.JokerAutoPrize, draw.TotalPremios);
        }

        [Fact]
        public void RecalculateFinancials_IncludesJokerCost_WhenEnabledWithoutPrize()
        {
            var plan = new Plan { CostPerBet = 1m, EnableJoker = true, JokerCostPerBet = 0.5m };
            var draw = new DrawRecord
            {
                Plan = plan,
                Played = true
            };

            draw.RecalculateFinancials(refreshCostsFromPlan: true);

            Assert.Equal(3m, draw.TotalCoste);
            Assert.Equal(0m, draw.TotalPremios);
            Assert.Equal(-3m, draw.Neto);
        }

        [Fact]
        public void RecalculateFinancials_ExcludesJoker_WhenDisabled()
        {
            var plan = new Plan { CostPerBet = 1m, EnableJoker = false };
            var draw = new DrawRecord
            {
                Plan = plan,
                Played = true,
                FixedPrize = 5m,
                JokerFixedPrize = 100m,
                JokerAutoPrize = 200m
            };

            draw.RecalculateFinancials(refreshCostsFromPlan: true);

            Assert.Equal(2m, draw.TotalCoste);
            Assert.Equal(5m, draw.TotalPremios);
            Assert.Equal(0m, draw.JokerFixedPrize);
            Assert.Equal(0m, draw.JokerAutoPrize);
            Assert.Equal(3m, draw.Neto);
        }

        [Fact]
        public void RecalculateFinancials_ResetsEveryComponent_WhenNotPlayed()
        {
            var draw = new DrawRecord
            {
                Played = false,
                CosteFija = 1m,
                CosteAuto = 2m,
                CosteJokerFija = 3m,
                CosteJokerAuto = 4m,
                FixedPrize = 10m,
                AutoPrize = 20m,
                JokerFixedPrize = 30m,
                JokerAutoPrize = 40m
            };

            draw.RecalculateFinancials();

            Assert.Equal(0m, draw.TotalCoste);
            Assert.Equal(0m, draw.TotalPremios);
            Assert.Equal(0m, draw.Neto);
        }
    }
}
