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
    }
}
