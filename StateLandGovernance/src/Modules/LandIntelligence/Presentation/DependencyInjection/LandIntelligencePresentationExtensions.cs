using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DependencyInjection;
using StateLandGovernance.LandIntelligence.Presentation.Middleware;

namespace StateLandGovernance.LandIntelligence.Presentation.DependencyInjection;

public static class LandIntelligencePresentationExtensions
{
    public static IServiceCollection AddLandIntelligencePresentation(this IServiceCollection services)
    {
        services.AddLandIntelligenceApplication();

        services.AddControllers()
            .AddApplicationPart(typeof(AssemblyReference).Assembly);

        return services;
    }

    public static IApplicationBuilder UseLandIntelligenceExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<LandIntelligenceExceptionHandlerMiddleware>();
}
