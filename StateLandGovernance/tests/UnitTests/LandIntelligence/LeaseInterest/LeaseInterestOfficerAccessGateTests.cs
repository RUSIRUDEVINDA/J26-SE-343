using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class LeaseInterestOfficerAccessGateTests
{
    [Theory]
    [InlineData("Development", true, true)]
    [InlineData("Testing", true, true)]
    [InlineData("Development", false, false)]
    [InlineData("Production", true, false)]
    [InlineData("Production", false, false)]
    public void Gate_RequiresNonProductionResearchDemo(
        string environment,
        bool researchDemoEnabled,
        bool expected)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
        var gate = new LeaseInterestOfficerAccessGate(new StubResearchDemoMode(researchDemoEnabled));
        Assert.Equal(expected, gate.IsNicLookupEnabled);
        Assert.Equal(expected, gate.IsPreliminaryReviewEnabled);
    }

    private sealed class StubResearchDemoMode(bool enabled) : IResearchDemoMode
    {
        public bool IsEnabled => enabled;
    }
}
