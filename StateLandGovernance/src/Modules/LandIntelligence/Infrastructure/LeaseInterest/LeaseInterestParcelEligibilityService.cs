using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Services;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class LeaseInterestParcelEligibilityService : ILeaseInterestParcelEligibilityService
{
    private readonly ILandParcelRepository _parcelRepository;
    private readonly LandParcelAvailabilityComposer _availabilityComposer;

    public LeaseInterestParcelEligibilityService(
        ILandParcelRepository parcelRepository,
        LandParcelAvailabilityComposer availabilityComposer)
    {
        _parcelRepository = parcelRepository;
        _availabilityComposer = availabilityComposer;
    }

    public async Task EnsureEligibleForLeaseInterestAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        await _availabilityComposer.EnsureLeaseInterestPermittedAsync(landParcelId, cancellationToken);

        var parcel = await _parcelRepository.GetByIdAsync(landParcelId, cancellationToken);
        if (parcel is null)
        {
            return;
        }

        var hard = HardConstraintEvaluator.Evaluate(parcel);
        if (hard.IsViolated)
        {
            throw new ValidationException(
            [
                "Lease interest cannot be submitted for this parcel: recorded hard restrictions apply. " +
                hard.Summary +
                " This is a preliminary screening block only — not a final legal determination.",
            ]);
        }
    }
}
