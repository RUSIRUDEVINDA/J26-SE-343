using Microsoft.AspNetCore.Http;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Presentation.Security;

namespace StateLandGovernance.LandIntelligence.Presentation.Middleware;

/// <summary>
/// Mirrors the authenticated applicant owner id into request items for diagnostics.
/// Owner identity is resolved from JWT claims or (development-only) the demo owner scheme.
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
        var ownerId = LandIntelligenceOwnerIdResolver.ResolveOwnerId(context);
        if (ownerId is { } id)
        {
            context.Items[LandIntelligenceCallerHeaders.OwnerIdItemKey] = id;
        }

        await _next(context);
    }
}
