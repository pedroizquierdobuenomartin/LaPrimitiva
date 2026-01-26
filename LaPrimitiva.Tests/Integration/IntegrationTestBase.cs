using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Xunit;
using LaPrimitiva.App;

namespace LaPrimitiva.Tests.Integration
{
    public class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
    {
        private readonly WebApplicationFactory<Program> _factory;
        private string? _connectionString;
        private DbConnection? _dbConnection;
        private Respawner? _respawner;

        public IntegrationTestBase(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
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
                
                _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
                {
                    TablesToIgnore = new Respawn.Graph.Table[]
                    {
                        "__EFMigrationsHistory"
                    }
                });
            }
        }

        public async Task ResetDatabaseAsync()
        {
            if (_respawner != null && _dbConnection != null)
            {
                await _respawner.ResetAsync(_dbConnection);
            }
        }

        public async Task DisposeAsync()
        {
            if (_dbConnection != null)
            {
                await _dbConnection.DisposeAsync();
            }
        }

        protected IServiceScope CreateScope() => _factory.Services.CreateScope();
    }
}
