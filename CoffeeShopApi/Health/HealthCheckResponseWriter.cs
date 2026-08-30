using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoffeeShopApi.Health;

internal static class HealthCheckResponseWriter
{
    public static Task WriteReadinessResponseAsync(HttpContext context, HealthReport report)
    {
        var response = new
        {
            status = report.Status == HealthStatus.Unhealthy ? "notReady" : "ready",
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Status.ToString().ToLowerInvariant())
        };

        return context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }
}

internal static class ReadinessHealthCheckOptions
{
    public const string RequiredTag = "ready";

    public static HealthCheckOptions Create() => new()
    {
        Predicate = check => check.Tags.Contains(RequiredTag),
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        },
        ResponseWriter = HealthCheckResponseWriter.WriteReadinessResponseAsync
    };
}
