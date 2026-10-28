using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

// Standalone module construction remains possible; no host identity means no routing authority.
public sealed class UnavailableComplaintDirectory : IComplaintOfficerDirectory
{
    public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct) => throw new CentralComplaintUnavailableException();
    public Task<bool> IsCommissionerAsync(Guid actor, CancellationToken ct) => Task.FromResult(false);
}
public sealed class UnavailableComplaintActor : IComplaintActorContext { public Guid ActorId => Guid.Empty; }
