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
}
