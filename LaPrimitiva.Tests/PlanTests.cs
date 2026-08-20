using Xunit;
using LaPrimitiva.Domain.Entities;
using System;

namespace LaPrimitiva.Tests
{
    public class PlanTests
    {
        [Fact]
        public void EnableJoker_WhenSetToFalse_ShouldSetJokerCostToZero()
        {
            // Arrange
            var plan = new Plan
            {
                EnableJoker = true,
                JokerCostPerBet = 1.00m
            };

            // Act
            plan.EnableJoker = false;

            // Assert
            Assert.Equal(0, plan.JokerCostPerBet);
        }

        [Fact]
        public void EnableJoker_WhenSetToTrue_AndCostIsZero_ShouldSetDefaultCost()
        {
            // Arrange
            var plan = new Plan
            {
                EnableJoker = false,
                JokerCostPerBet = 0
            };

            // Act
            plan.EnableJoker = true;

            // Assert
            Assert.Equal(1.00m, plan.JokerCostPerBet);
        }
        
        [Fact]
        public void EnableJoker_WhenSetToTrue_AndCostIsNotNullOrZero_ShouldKeepCurrentCost()
        {
            // Arrange
            var plan = new Plan
            {
                EnableJoker = false,
                JokerCostPerBet = 0.50m
            };

            // Act
            plan.EnableJoker = true;

            // Assert
            Assert.Equal(0.50m, plan.JokerCostPerBet);
        }

        [Theory]
        [MemberData(nameof(InvalidPlans))]
        public void Validate_ShouldRejectInvalidBusinessRules(Plan plan, string expectedMessage)
        {
            var exception = Assert.Throws<InvalidOperationException>(plan.Validate);

            Assert.Contains(expectedMessage, exception.Message);
        }

        [Fact]
        public void Validate_ShouldAcceptBoundaryValues()
        {
            var plan = new Plan
            {
                Name = "Plan válido",
                EffectiveFrom = new DateTime(2026, 1, 1),
                EffectiveTo = new DateTime(2026, 1, 1),
                WeeksToTrackDefault = 0,
                CostPerBet = 0,
                BetsPerDraw = Plan.MaxBetsPerDraw,
                EnableJoker = false,
                JokerCostPerBet = 0
            };

            plan.Validate();
        }

        public static TheoryData<Plan, string> InvalidPlans => new()
        {
            {
                WithDates(ValidPlan(), new DateTime(2026, 2, 1), new DateTime(2026, 1, 31)),
                "fecha final"
            },
            { ValidPlan(costPerBet: -0.01m), "coste por apuesta" },
            { ValidPlan(weeksToTrackDefault: -1), "semanas" },
            { ValidPlan(betsPerDraw: Plan.MinBetsPerDraw - 1), "apuestas por sorteo" },
            { ValidPlan(betsPerDraw: Plan.MaxBetsPerDraw + 1), "apuestas por sorteo" },
            { ValidPlan(enableJoker: true, jokerCostPerBet: -0.01m), "coste de Joker" },
            { ValidPlan(enableJoker: false, jokerCostPerBet: 0.50m), "Joker desactivado" }
        };

        private static Plan ValidPlan(
            decimal costPerBet = 1m,
            int weeksToTrackDefault = 52,
            int betsPerDraw = 2,
            bool enableJoker = false,
            decimal jokerCostPerBet = 0m) => new()
        {
            Name = "Plan válido",
            EffectiveFrom = new DateTime(2026, 1, 1),
            EffectiveTo = new DateTime(2026, 12, 31),
            CostPerBet = costPerBet,
            WeeksToTrackDefault = weeksToTrackDefault,
            BetsPerDraw = betsPerDraw,
            EnableJoker = enableJoker,
            JokerCostPerBet = jokerCostPerBet
        };

        private static Plan WithDates(Plan plan, DateTime from, DateTime to)
        {
            plan.EffectiveFrom = from;
            plan.EffectiveTo = to;
            return plan;
        }
    }
}
