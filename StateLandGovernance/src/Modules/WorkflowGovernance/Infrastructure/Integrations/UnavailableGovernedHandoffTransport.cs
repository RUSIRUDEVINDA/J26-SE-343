namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class UnavailableGovernedHandoffTransport : IGovernedHandoffTransport
{
    public Task<HandoffTransportContract?> ReadContractAsync(CancellationToken ct) => Task.FromResult<HandoffTransportContract?>(null);
    public Task<HandoffTransportResult> DispatchAsync(HandoffTransportRequest request, CancellationToken ct) =>
        throw new InvalidOperationException("Downstream recipient contract and authenticated transport are not configured.");
}
