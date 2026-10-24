namespace StateLandGovernance.WorkflowGovernance.Presentation;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.WorkflowGovernance.Presentation.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowGovernancePresentation(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<OcrDemoOptions>? configureOptions = null)
    {
        services.AddOptions<OcrDemoOptions>();

        if (configuration is not null)
        {
            services.Configure<OcrDemoOptions>(
                configuration.GetSection(OcrDemoOptions.SectionName));
        }

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        return services;
    }
}
