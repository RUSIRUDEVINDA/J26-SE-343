using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class LeaseInterestStatusTransitionsTests
{
    [Theory]
    [InlineData(LeaseInterestRequestStatus.PendingVerification, LeaseInterestRequestStatus.NeedsInformation, true)]
    [InlineData(LeaseInterestRequestStatus.PendingVerification, LeaseInterestRequestStatus.Declined, true)]
    [InlineData(LeaseInterestRequestStatus.PendingVerification, LeaseInterestRequestStatus.ReferredToAuthority, true)]
    [InlineData(LeaseInterestRequestStatus.NeedsInformation, LeaseInterestRequestStatus.PendingVerification, true)]
    [InlineData(LeaseInterestRequestStatus.Draft, LeaseInterestRequestStatus.PendingVerification, false)]
    [InlineData(LeaseInterestRequestStatus.PendingVerification, LeaseInterestRequestStatus.Draft, false)]
    [InlineData(LeaseInterestRequestStatus.Declined, LeaseInterestRequestStatus.PendingVerification, false)]
    public void TryGetTransition_MatchesPilotReviewMatrix(
        LeaseInterestRequestStatus current,
        LeaseInterestRequestStatus next,
        bool expectedAllowed)
    {
        var allowed = LeaseInterestStatusTransitions.TryGetTransition(current, next, out var reason);
        Assert.Equal(expectedAllowed, allowed);
        if (!expectedAllowed)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }
    }

    [Theory]
    [InlineData(
        LeaseInterestRequestStatus.ReferredToAuthority,
        LeaseInterestAuthorityDecisionKind.Approved,
        LeaseInterestRequestStatus.ApprovedInPrinciple,
        true)]
    [InlineData(
        LeaseInterestRequestStatus.ReferredToAuthority,
        LeaseInterestAuthorityDecisionKind.Rejected,
        LeaseInterestRequestStatus.Declined,
        true)]
    [InlineData(
        LeaseInterestRequestStatus.PendingVerification,
        LeaseInterestAuthorityDecisionKind.Approved,
        LeaseInterestRequestStatus.PendingVerification,
        false)]
    public void TryGetAuthorityTransition_OnlyApprovedOrRejectedFromReferred(
        LeaseInterestRequestStatus current,
        LeaseInterestAuthorityDecisionKind decision,
        LeaseInterestRequestStatus expectedNext,
        bool expectedAllowed)
    {
        var allowed = LeaseInterestStatusTransitions.TryGetAuthorityTransition(
            current,
            decision,
            out var next,
            out var reason);
        Assert.Equal(expectedAllowed, allowed);
        Assert.Equal(expectedNext, next);
        if (!expectedAllowed)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }
    }

    [Fact]
    public void ApplicantOutcomeSummary_Approved_RequiresFormalLeaseProcessing()
    {
        var summary = LeaseInterestStatusTransitions.ApplicantOutcomeSummary(
            LeaseInterestRequestStatus.ApprovedInPrinciple);
        Assert.Contains("Formal lease processing is still required", summary, StringComparison.Ordinal);
        Assert.Contains("Approved", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionLabels_AreApprovedAndRejectedOnly()
    {
        Assert.Equal("Approved", LeaseInterestAuthorityDecisionLabels.DecisionLabel(
            LeaseInterestAuthorityDecisionKind.Approved));
        Assert.Equal("Rejected", LeaseInterestAuthorityDecisionLabels.DecisionLabel(
            LeaseInterestAuthorityDecisionKind.Rejected));
    }
}
