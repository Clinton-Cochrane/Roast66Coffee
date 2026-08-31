using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace CoffeeShopApi.Middleware;

/// <summary>
/// Emits one request-completion event without recording raw paths, query values,
/// or exception messages that may contain credentials or customer data.
/// </summary>
public sealed class SafeRequestLoggingMiddleware
{
    private const string UnmatchedRoute = "<unmatched>";
    private readonly RequestDelegate _next;
    private readonly ILogger<SafeRequestLoggingMiddleware> _logger;

    public SafeRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<SafeRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            LogFailure(context, stopwatch.Elapsed.TotalMilliseconds, ex.GetType().Name);
            throw;
        }

        LogCompletion(context, stopwatch.Elapsed.TotalMilliseconds);
    }

    private void LogCompletion(HttpContext context, double elapsedMilliseconds)
    {
        var statusCode = context.Response.StatusCode;
        var level = statusCode >= StatusCodes.Status500InternalServerError
            ? LogLevel.Error
            : LogLevel.Information;

        _logger.Log(
            level,
            "HTTP {RequestMethod} {RouteTemplate} responded {StatusCode} in {ElapsedMilliseconds:0.0000} ms with trace {TraceId}.",
            GetSafeRequestMethod(context.Request.Method),
            GetRouteTemplate(context),
            statusCode,
            elapsedMilliseconds,
            context.TraceIdentifier);
    }

    private void LogFailure(HttpContext context, double elapsedMilliseconds, string failureType)
    {
        _logger.LogError(
            "HTTP {RequestMethod} {RouteTemplate} responded {StatusCode} in {ElapsedMilliseconds:0.0000} ms with trace {TraceId}. Failure type: {FailureType}.",
            GetSafeRequestMethod(context.Request.Method),
            GetRouteTemplate(context),
            StatusCodes.Status500InternalServerError,
            elapsedMilliseconds,
            context.TraceIdentifier,
            failureType);
    }

    private static string GetSafeRequestMethod(string method) =>
        method switch
        {
            "GET" => "GET",
            "POST" => "POST",
            "PUT" => "PUT",
            "PATCH" => "PATCH",
            "DELETE" => "DELETE",
            "HEAD" => "HEAD",
            "OPTIONS" => "OPTIONS",
            _ => "OTHER"
        };

    private static string GetRouteTemplate(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? UnmatchedRoute;
}
