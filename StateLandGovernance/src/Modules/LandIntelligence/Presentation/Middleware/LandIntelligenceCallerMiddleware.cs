using Microsoft.AspNetCore.Http;
using StateLandGovernance.LandIntelligence.Application.Configuration;

namespace StateLandGovernance.LandIntelligence.Presentation.Middleware;

/// <summary>
/// Resolves a stable owner id from a trusted header for development and integration
/// until platform-wide authentication is wired. Never read owner id from JSON bodies.
/// </summary>
public sealed class LandIntelligenceCallerMiddleware
{
    private readonly RequestDelegate _next;

    public LandIntelligenceCallerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(
                LandIntelligenceCallerHeaders.OwnerIdHeaderName,
                out var headerValues))
        {
            var raw = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out var ownerId) && ownerId != Guid.Empty)
            {
                context.Items[LandIntelligenceCallerHeaders.OwnerIdItemKey] = ownerId;
            }
        }

        await _next(context);
    }
}
