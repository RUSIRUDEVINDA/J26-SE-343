using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Verifies parcel administrative location against imported GIS boundary reference layers.
/// Does not overwrite official parcel Province/District values.
/// </summary>
public interface IAdministrativeLocationVerificationService
{
    Task<AdministrativeLocationVerificationResult> VerifyAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
