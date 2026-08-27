using System;
using System.Linq;
using Xunit;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;
using StateLandGovernance.LeaseFeasibility.Domain.Events;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Domain;

public class LeaseProposalTests
{
    [Fact]
    public void LeaseProposal_WithValidInputs_InitializesCorrectly()
    {
        var proposal = new LeaseProposal("APP-123", 10.5m, 20, 50000m, 0.95m);
        Assert.Equal("APP-123", proposal.ApplicationId);
        Assert.False(proposal.IsFinalized);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void LeaseProposal_WithInvalidDuration_ThrowsArgumentOutOfRangeException(int duration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LeaseProposal("APP-123", 10.5m, duration, 50000m, 0.95m));
    }

    [Fact]
    public void FinalizeProposal_WhenCalled_SetsIsFinalizedAndRaisesEvent()
    {
        var proposal = new LeaseProposal("APP-123", 10.5m, 20, 50000m, 0.95m);
        proposal.FinalizeProposal();

        Assert.True(proposal.IsFinalized);
        var evt = proposal.DomainEvents.SingleOrDefault() as LeaseProposalGeneratedEvent;
        Assert.NotNull(evt);
        Assert.Equal(proposal.Id, evt.ProposalId);
    }
}
