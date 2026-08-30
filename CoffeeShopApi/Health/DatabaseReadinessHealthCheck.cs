using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoffeeShopApi.Health;

internal interface IDatabaseReadinessProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}

internal sealed class EfCoreDatabaseReadinessProbe(ApplicationDbContext context)
    : IDatabaseReadinessProbe
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        context.Database.CanConnectAsync(cancellationToken);
}

internal sealed class DatabaseReadinessHealthCheck(
    IDatabaseReadinessProbe probe,
    ILogger<DatabaseReadinessHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await probe.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Healthy();
            }

            logger.LogWarning("Database readiness check reported that the database is unavailable.");
            return HealthCheckResult.Unhealthy("The required database is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Database readiness check was canceled or timed out.");
            return HealthCheckResult.Unhealthy("The required database did not respond in time.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Database readiness check failed. Failure type: {FailureType}.",
                ex.GetType().Name);
            return HealthCheckResult.Unhealthy("The required database is unavailable.");
        }
    }
}
