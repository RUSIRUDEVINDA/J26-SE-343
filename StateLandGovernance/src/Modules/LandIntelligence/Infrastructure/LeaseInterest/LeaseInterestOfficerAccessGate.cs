using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class LeaseInterestOfficerAccessGate : ILeaseInterestOfficerAccessGate
{
    public LeaseInterestOfficerAccessGate(IResearchDemoMode researchDemoMode)
    {
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var isProduction = string.Equals(
            environment,
            "Production",
            StringComparison.OrdinalIgnoreCase);
        var isDevelopmentOrTesting =
            string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase);

        var pilotOfficerToolsEnabled =
            !isProduction && isDevelopmentOrTesting && researchDemoMode.IsEnabled;

        IsNicLookupEnabled = pilotOfficerToolsEnabled;
        IsPreliminaryReviewEnabled = pilotOfficerToolsEnabled;
    }

    public bool IsNicLookupEnabled { get; }

    public bool IsPreliminaryReviewEnabled { get; }
}
