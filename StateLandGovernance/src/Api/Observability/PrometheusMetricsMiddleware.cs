using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using StateLandGovernance.BuildingBlocks.Observability;

namespace StateLandGovernance.Api.Observability;

public sealed class PrometheusMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public PrometheusMetricsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, PrometheusMetricsRegistry metrics)
    {
        if (context.Request.Path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                ?? context.Request.Path.Value
                ?? "unmatched";
            metrics.RecordHttpRequest(
                context.Request.Method,
                route,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt));
        }
    }
}
