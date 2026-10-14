using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.EarlyGovernanceReferral;

/// <summary>
/// Development-only synthetic acknowledgement. It records that normal-flow exit was requested
/// for PP1 evidence, but deliberately does not claim that Component 3 was contacted or terminated.
/// </summary>
public sealed class SyntheticPp1ReferralHandoff : IEarlyGovernanceReferralHandoff
{
    public Task<EarlyGovernanceReferralHandoffResult> SendAsync(
        EarlyGovernanceReferralHandoffRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var reference = $"SyntheticPp1Handoff:NormalFlowExitRequested:{request.CorrelationId:D}";
        return Task.FromResult(new EarlyGovernanceReferralHandoffResult(
            EarlyGovernanceReferralHandoffOutcome.Acknowledged,
            reference));
    }
}
