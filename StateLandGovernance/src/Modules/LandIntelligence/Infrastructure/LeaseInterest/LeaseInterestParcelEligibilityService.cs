using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Application.Services;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class LeaseInterestParcelEligibilityService : ILeaseInterestParcelEligibilityService
{
    private readonly ILandParcelRepository _parcelRepository;
    private readonly LandParcelAvailabilityComposer _availabilityComposer;
    private readonly IApprovedPreliminaryLeaseParcelRegistry _approvedPreliminaryRegistry;

    public LeaseInterestParcelEligibilityService(
        ILandParcelRepository parcelRepository,
        LandParcelAvailabilityComposer availabilityComposer,
        IApprovedPreliminaryLeaseParcelRegistry approvedPreliminaryRegistry)
    {
        _parcelRepository = parcelRepository;
        _availabilityComposer = availabilityComposer;
        _approvedPreliminaryRegistry = approvedPreliminaryRegistry;
    }

    public async Task EnsureEligibleForLeaseInterestAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        if (await _approvedPreliminaryRegistry.HasApprovedPreliminaryRequestAsync(
                landParcelId,
                cancellationToken))
        {
            throw new ValidationException([ApprovedPreliminaryLeaseParcelPolicy.BlockNewLeaseInterestMessage]);
        }

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
