using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LaPrimitiva.App.Observability;

public sealed class DatabaseHealthCheck(
    IDbContextFactory<PrimitivaDbContext> contextFactory,
    ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (canConnect)
            {
                return HealthCheckResult.Healthy("La base de datos está disponible.");
            }

            logger.LogWarning("El health check de base de datos no pudo establecer conexión.");
            return HealthCheckResult.Unhealthy("La base de datos no está disponible.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falló el health check de base de datos.");
            return HealthCheckResult.Unhealthy("La base de datos no está disponible.");
        }
    }
}
