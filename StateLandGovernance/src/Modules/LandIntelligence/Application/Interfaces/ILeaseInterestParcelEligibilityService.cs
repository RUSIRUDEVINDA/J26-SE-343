namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestParcelEligibilityService
{
    Task EnsureEligibleForLeaseInterestAsync(Guid landParcelId, CancellationToken cancellationToken = default);
}
