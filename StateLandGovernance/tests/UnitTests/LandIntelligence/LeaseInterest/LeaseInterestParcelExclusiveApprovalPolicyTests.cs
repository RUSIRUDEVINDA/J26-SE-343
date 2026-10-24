using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class LeaseInterestParcelExclusiveApprovalPolicyTests
{
    [Theory]
    [InlineData(LeaseInterestRequestStatus.Draft, true)]
    [InlineData(LeaseInterestRequestStatus.Submitted, true)]
    [InlineData(LeaseInterestRequestStatus.PendingVerification, true)]
    [InlineData(LeaseInterestRequestStatus.NeedsInformation, true)]
    [InlineData(LeaseInterestRequestStatus.ReferredToAuthority, true)]
    [InlineData(LeaseInterestRequestStatus.Declined, false)]
    [InlineData(LeaseInterestRequestStatus.ApprovedInPrinciple, false)]
    [InlineData(LeaseInterestRequestStatus.ClosedAnotherPreliminaryApproved, false)]
    public void IsOpenForParcelExclusiveClosure_MatchesResearchWorkflow(
        LeaseInterestRequestStatus status,
        bool expectedOpen)
    {
        Assert.Equal(
            expectedOpen,
            LeaseInterestParcelExclusiveApprovalPolicy.IsOpenForParcelExclusiveClosure(status));
    }

    [Fact]
    public void ApplicantOutcomeSummary_ClosedAnotherPreliminaryApproved_IsClear()
    {
        var summary = LeaseInterestStatusTransitions.ApplicantOutcomeSummary(
            LeaseInterestRequestStatus.ClosedAnotherPreliminaryApproved);
        Assert.Contains("Closed", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a rejection", summary, StringComparison.OrdinalIgnoreCase);
    }
}
