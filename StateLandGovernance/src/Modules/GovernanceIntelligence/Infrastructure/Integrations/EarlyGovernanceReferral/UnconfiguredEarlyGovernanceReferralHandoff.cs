using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.EarlyGovernanceReferral;

/// <summary>
/// Safe production placeholder until Component 3 publishes and implements the agreed boundary.
/// It never claims that a workflow ended or a Commissioner review started.
/// </summary>
public sealed class UnconfiguredEarlyGovernanceReferralHandoff : IEarlyGovernanceReferralHandoff
{
    public Task<EarlyGovernanceReferralHandoffResult> SendAsync(
        EarlyGovernanceReferralHandoffRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new EarlyGovernanceReferralHandoffResult(
            EarlyGovernanceReferralHandoffOutcome.NotConfigured,
            FailureCode: "Component3ReferralHandoffNotConfigured"));
    }
}
