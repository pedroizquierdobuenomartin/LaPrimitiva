using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LaPrimitiva.Application.DTOs;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using LaPrimitiva.App;
using Xunit;

namespace LaPrimitiva.Tests.Integration
{
    public class PlanIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public PlanIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetPlansByYearAsync_ShouldReturnPlans_FromDatabase()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var planService = scope.ServiceProvider.GetRequiredService<PlanService>();
            var planRepo = scope.ServiceProvider.GetRequiredService<IPlanRepository>();

            var year = 2027;
            var testPlan = new Plan 
            { 
                Name = "Integration Test Plan", 
                EffectiveFrom = new DateTime(year, 1, 1),
                EffectiveTo = new DateTime(year, 12, 31)
            };
            await planRepo.CreateAsync(testPlan);

            // Act
            var results = await planService.GetPlansByYearAsync(year);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, p => p.Name == "Integration Test Plan");
        }
    }
}
