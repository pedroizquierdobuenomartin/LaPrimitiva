using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using LaPrimitiva.App;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LaPrimitiva.Tests.Integration
{
    // Removed IClassFixture from here to avoid xUnit1041. 
    // Subclasses should implement IClassFixture<WebApplicationFactory<Program>>.
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected readonly WebApplicationFactory<LaPrimitiva.App.Program> _factory;
        private string? _connectionString;
        private DbConnection? _dbConnection;
        private IDbContextTransaction? _transaction;

        protected IntegrationTestBase(WebApplicationFactory<LaPrimitiva.App.Program> factory)
        {
            // We use the base factory and customize it per instance if needed, 
            // but the transactional integrity is what matters most here.
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PrimitivaDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<PrimitivaDbContext>(options =>
                    {
                        if (_dbConnection != null)
                        {
                            options.UseSqlServer(_dbConnection);
                        }
                    });
                });
            });
        }

        public async Task InitializeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            _connectionString = configuration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrEmpty(_connectionString))
            {
                _dbConnection = new SqlConnection(_connectionString);
                await _dbConnection.OpenAsync();
                
                using var innerScope = _factory.Services.CreateScope();
                var context = innerScope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
                _transaction = await context.Database.BeginTransactionAsync();
            }
        }

        public async Task ResetDatabaseAsync()
        {
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
            }

            if (_dbConnection != null)
            {
                await _dbConnection.DisposeAsync();
            }
        }

        protected IServiceScope CreateScope() => _factory.Services.CreateScope();
    }
}
