using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Configuration;

public sealed class ResearchDemoMode : IResearchDemoMode
{
    public ResearchDemoMode(IOptions<ResearchDemoOptions> options) =>
        IsEnabled = options.Value.Enabled;

    public bool IsEnabled { get; }
}
