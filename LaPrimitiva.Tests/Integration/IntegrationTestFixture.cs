using System.Net;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace LaPrimitiva.Tests.Integration;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly string _connectionString =
        IntegrationTestDatabase.CreateIsolatedConnectionString(
            IntegrationTestDatabase.GetConnectionString());

    private Respawner? _respawner;

    public WebApplicationFactory<LaPrimitiva.App.Program> Factory { get; private set; } = null!;

    public string TestDataDirectory => Path.Combine(AppContext.BaseDirectory, "TestData");

    public async ValueTask InitializeAsync()
    {
        IntegrationTestDatabase.EnsureSafe(_connectionString);

        try
        {
            await using var context = CreateDbContext();
            await context.Database.MigrateAsync();
        }
        catch
        {
            await DeleteDatabaseAsync();
            throw;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });

        Factory = new IntegrationTestApplicationFactory(_connectionString);
    }

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException("La base de integración no se ha inicializado.");
        }

        IntegrationTestDatabase.EnsureSafe(_connectionString);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        Factory?.Dispose();
        await DeleteDatabaseAsync();
    }

    private PrimitivaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new PrimitivaDbContext(options);
    }

    private async Task DeleteDatabaseAsync()
    {
        IntegrationTestDatabase.EnsureSafe(_connectionString);
        SqlConnection.ClearAllPools();

        await using var context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
    }

    private sealed class IntegrationTestApplicationFactory(string connectionString)
        : WebApplicationFactory<LaPrimitiva.App.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTests");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, LoopbackConnectionStartupFilter>());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString
                });
            });
        }
    }

    private sealed class LoopbackConnectionStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Loopback;
                return nextMiddleware();
            });
            next(app);
        };
    }
}
