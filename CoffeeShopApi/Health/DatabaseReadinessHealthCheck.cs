using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoffeeShopApi.Health;

internal interface IDatabaseReadinessProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken);
}

internal sealed class EfCoreDatabaseReadinessProbe(ApplicationDbContext context)
    : IDatabaseReadinessProbe
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        context.Database.CanConnectAsync(cancellationToken);

    public async Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
        {
            return false;
        }

        return (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
    }
}

/// <summary>
/// Gates traffic on the two database properties required for safe service: a live
/// connection and a schema with no pending migrations. Optional email, payment,
/// push, SMS, and keepalive integrations intentionally do not affect readiness.
/// </summary>
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
            if (!await probe.CanConnectAsync(cancellationToken))
            {
                logger.LogWarning("Database readiness check reported that the database is unavailable.");
                return HealthCheckResult.Unhealthy("The required database is unavailable.");
            }

            if (await probe.HasPendingMigrationsAsync(cancellationToken))
            {
                logger.LogWarning("Database readiness check found pending schema migrations.");
                return HealthCheckResult.Unhealthy("The required database schema is not current.");
            }

            return HealthCheckResult.Healthy();
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
