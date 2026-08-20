using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaPrimitiva.Tests.Integration
{
    // Removed IClassFixture from here to avoid xUnit1041. 
    // Subclasses should implement IClassFixture<WebApplicationFactory<Program>>.
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected readonly WebApplicationFactory<LaPrimitiva.App.Program> _factory;

        protected IntegrationTestBase(WebApplicationFactory<LaPrimitiva.App.Program> factory)
        {
            var connectionString = IntegrationTestDatabase.GetConnectionString();

            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTests");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = connectionString
                    });
                });
            });
        }

        public Task InitializeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            IntegrationTestDatabase.EnsureSafe(
                configuration.GetConnectionString("DefaultConnection"));

            return Task.CompletedTask;
        }

        public async Task ResetDatabaseAsync()
        {
            await Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        protected IServiceScope CreateScope() => _factory.Services.CreateScope();
    }
}
